using System.Data;
using System.Text.Json;
using AutoMapper;
using ECafe.Application.Common.Audit;
using ECafe.Application.Common.Exceptions;
using ECafe.Application.DTOs.Notification;
using ECafe.Application.DTOs.Payment;
using ECafe.Application.Mappings;
using ECafe.Application.Repositories.Table;
using ECafe.Application.Repositories.TableSession;
using ECafe.Application.Repositories.User;
using ECafe.Application.Repositories.UserRestaurant;
using ECafe.Application.Repository;
using ECafe.Application.Services.AuditLog.Abstract;
using ECafe.Application.Services.Notification.Abstract;
using ECafe.Application.Services.Payments.Abstract;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using ECafe.Domain.Policies;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using StatusType = ECafe.Domain.Enums.StatusType;

namespace ECafe.Application.Services.Payments.Concrete;

public sealed class TableSessionBillingManager(IHttpContextAccessor accessor, IMapper mapper, IConfiguration configuration,
    ITableSessionRepository sessions, IBaseRepository<Payment> payments, ITableRepository tables,
    IUserRepository users, IUserRestaurantRepository assignments, IApplicationDbTransactionFactory transactions,
    IPaymentGateway gateway, PaymentRuntime runtime, TimeProvider clock, IAuditLogService audit,
    INotificationService notifications) : BaseManager(accessor, mapper, configuration), ITableSessionBillingService
{
    public async Task<SessionBillResponse> GetAsync(int tableSessionId, CancellationToken cancellationToken)
    {
        var session = await FindAsync(tableSessionId, false, cancellationToken);
        var customer = await RequireAccessAsync(session, cancellationToken);
        return SessionBillMapper.Map(session, runtime.GetCapabilities(), customer);
    }

    public async Task<SessionBillPaymentResponse> CheckoutAsync(int tableSessionId, string key, CancellationToken cancellationToken)
    {
        runtime.EnsureEnabled();
        runtime.EnsureSimulation(gateway.IsTest);
        if (!Guid.TryParseExact(key, "D", out var parsed) || parsed == Guid.Empty)
            throw new BadRequestException(ErrorCode.PaymentRequestKeyRequired);
        key = parsed.ToString("D");
        var snapshot = await FindAsync(tableSessionId, false, cancellationToken);
        await RequireAccessAsync(snapshot, cancellationToken);
        await using var transaction = await transactions.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var session = await LockAsync(snapshot, cancellationToken);
        var userId = GetCurrentUserId();
        var existing = await payments.Query(payment => payment.CreatedByUserId == userId && payment.CheckoutKey == key).IgnoreQueryFilters()
            .SingleOrDefaultAsync(cancellationToken);
        if (existing != null)
        {
            if (existing.IsDeleted || existing.TableSessionId != tableSessionId || existing.RestaurantId != session.RestaurantId ||
                existing.ReservationId != null || existing.OrderId != null)
                throw new BusinessRuleException(ErrorCode.PaymentRequestKeyConflict);
            await transaction.CommitAsync(cancellationToken);
            return SessionBillMapper.Map(existing);
        }
        var bill = TableSessionBillingPolicy.Calculate(session);
        if (bill.RequiresReview) throw new BusinessRuleException(ErrorCode.SessionBillInvalid);
        if (!bill.IsOpen || bill.RemainingAmount <= 0) throw new BusinessRuleException(ErrorCode.PaymentTransitionNotAllowed);
        if (bill.HasUnservedOrders) throw new BusinessRuleException(ErrorCode.SessionBillOrdersUnserved);
        if (bill.HasPendingPayment) throw new BusinessRuleException(ErrorCode.SessionBillPaymentPending);
        var payment = new Payment
        {
            RestaurantId = session.RestaurantId, TableSessionId = session.Id, TableSession = session,
            CreatedByUserId = userId, Amount = bill.RemainingAmount, CurrencyCode = TableSessionBillingPolicy.Currency,
            CheckoutKey = key, BillFingerprint = bill.Fingerprint, IsTest = gateway.IsTest, ProviderName = gateway.Name,
            PaymentMethodId = StatusIds.Build(StatusType.PaymentMethod, (int)PaymentMethod.Online),
            PaymentStatusId = StatusIds.Build(StatusType.PaymentStatus, (int)PaymentStatus.Pending)
        };
        await payments.Add(payment);
        await payments.SaveChangesAsync();
        await gateway.CreateCheckoutAsync(payment, cancellationToken);
        await payments.SaveChangesAsync();
        await audit.RecordRestaurantActionAsync(session.RestaurantId, AuditActions.SessionBillCheckoutCreated,
            new { tableSessionId, paymentId = payment.Id, payment.Amount, payment.IsTest, bill.TotalAmount, bill.DepositCreditAmount },
            AuditEntityTypes.TableSession, session.Id);
        await transaction.CommitAsync(cancellationToken);
        return SessionBillMapper.Map(payment);
    }

    public Task<SessionBillPaymentResponse> RefreshAsync(int tableSessionId, int paymentId, CancellationToken cancellationToken)
        => ReconcileAsync(tableSessionId, paymentId, null, cancellationToken);

    public Task<SessionBillPaymentResponse> SimulateAsync(int tableSessionId, int paymentId, ProviderOperationStatus outcome, CancellationToken cancellationToken)
        => ReconcileAsync(tableSessionId, paymentId, outcome, cancellationToken);

    /// <summary>Browser statusuna etibar etmir; məbləğ və provayderi yoxlayaraq eyni masa kilidində bir dəfə təsdiq və audit yaradır.</summary>
    private async Task<SessionBillPaymentResponse> ReconcileAsync(int tableSessionId, int paymentId,
        ProviderOperationStatus? outcome, CancellationToken cancellationToken)
    {
        runtime.EnsureEnabled();
        var snapshot = await FindAsync(tableSessionId, false, cancellationToken);
        await RequireAccessAsync(snapshot, cancellationToken);
        await using var transaction = await transactions.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var session = await LockAsync(snapshot, cancellationToken);
        var payment = session.Payments.SingleOrDefault(item => item.Id == paymentId && !item.IsDeleted &&
            item.RestaurantId == session.RestaurantId && item.ReservationId == null && item.OrderId == null)
            ?? throw new NotFoundException(ErrorCode.PaymentNotFound);
        runtime.EnsureSimulation(payment.IsTest);
        if (payment.ProviderName != gateway.Name || payment.IsTest != gateway.IsTest)
            throw new BusinessRuleException(ErrorCode.PaymentProviderMismatch);
        if (outcome.HasValue)
        {
            if (!Enum.IsDefined(outcome.Value)) throw new BadRequestException(ErrorCode.PaymentTransitionNotAllowed);
            if (payment.PaymentStatusId == StatusIds.Build(StatusType.PaymentStatus, (int)PaymentStatus.Pending))
                payment.ProviderStatus = outcome.Value;
        }
        var result = await gateway.VerifyPaymentAsync(payment, cancellationToken);
        ProviderPaymentPolicy.ValidateResult(payment, result.Reference, result.Amount, result.CurrencyCode, result.IsTest);
        if (TableSessionBillingPolicy.ApplyPayment(session, payment, result.Status, clock.GetUtcNow().UtcDateTime))
        {
            await payments.SaveChangesAsync();
            await audit.RecordRestaurantActionAsync(session.RestaurantId, AuditActions.SessionBillPaymentUpdated,
                new { tableSessionId, paymentId, payment.Amount, payment.PaymentStatusId, payment.IsTest, payment.RequiresRefund },
                AuditEntityTypes.TableSession, session.Id);
            if (payment.PaidAt.HasValue) await NotifyPaidAsync(session, payment, cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return SessionBillMapper.Map(payment);
    }

    /// <summary>Hesab üçün sifariş snapshotlarını və yalnız həmin sessiyanın ödənişlərini bir neçə sabit sorğu ilə yükləyir.</summary>
    private async Task<TableSession> FindAsync(int id, bool tracked, CancellationToken cancellationToken)
    {
        if (id <= 0) throw new BadRequestException(ErrorCode.InvalidTableSessionId);
        return await sessions.GetForBillingAsync(id, tracked, cancellationToken)
            ?? throw new NotFoundException(ErrorCode.TableSessionNotFound);
    }

    /// <summary>Sifariş yaradılması və bağlanış ilə ortaq masa kilidini alır; gözləmədən sonra canlı rol və aidiyyəti yenidən yoxlayır.</summary>
    private async Task<TableSession> LockAsync(TableSession snapshot, CancellationToken cancellationToken)
    {
        await tables.AcquireReservationLockAsync(snapshot.RestaurantId, snapshot.TableId, cancellationToken);
        var session = await FindAsync(snapshot.Id, true, cancellationToken);
        await RequireAccessAsync(session, cancellationToken);
        if (session.TableId != snapshot.TableId || session.RestaurantId != snapshot.RestaurantId)
            throw new BusinessRuleException(ErrorCode.OrderSessionChanged);
        return session;
    }

    /// <summary>Öz müştərisinə və cari məsul ofisiant/menecerə giriş verir; aktiv hesabı və restoranın canlı təyinatını yoxlayır.</summary>
    private async Task<bool> RequireAccessAsync(TableSession session, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (!await users.Query(user => user.Id == userId && user.IsActive).AnyAsync(cancellationToken))
            throw new ForbiddenException(ErrorCode.AccessDenied);
        if (GetCurrentRoleId() == (int)RoleCode.Customer)
        {
            if (session.CustomerUserId != userId) throw new NotFoundException(ErrorCode.TableSessionNotFound);
            return true;
        }
        EnsureCurrentUserCanAccessRestaurant(session.RestaurantId);
        var role = await assignments.GetActiveRoleIdAsync(userId, session.RestaurantId);
        if (role is (int)RoleCode.Owner or (int)RoleCode.Manager) return false;
        if (role == (int)RoleCode.Waiter && session.WaiterUserId == userId) return false;
        throw new NotFoundException(ErrorCode.TableSessionNotFound);
    }

    /// <summary>Təsdiqlənmiş test hesabını müştəriyə və cari məsul ofisianta mövcud bildiriş/push outbox yolu ilə bir dəfə çatdırır.</summary>
    private async Task NotifyPaidAsync(TableSession session, Payment payment, CancellationToken cancellationToken)
    {
        var recipients = new HashSet<int>();
        if (session.CustomerUserId.HasValue) recipients.Add(session.CustomerUserId.Value);
        if (session.WaiterUserId.HasValue && await assignments.GetActiveRoleIdAsync(session.WaiterUserId.Value, session.RestaurantId) == (int)RoleCode.Waiter)
            recipients.Add(session.WaiterUserId.Value);
        var staff = await assignments.GetActiveByRestaurantAndRolesAsync(session.RestaurantId, [(int)RoleCode.Manager, (int)RoleCode.Owner]);
        foreach (var assignment in staff) recipients.Add(assignment.UserId);
        foreach (var userId in recipients)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await notifications.CreateAsync(new CreateNotificationRequest
            {
                UserId = userId, RestaurantId = session.RestaurantId, TypeId = (int)NotificationType.TableSessionPaid,
                ChannelId = (int)NotificationChannel.InApp, RelatedEntityType = AuditEntityTypes.TableSession, RelatedEntityId = session.Id,
                Title = payment.RequiresRefund ? SessionBillTexts.ReviewTitle : SessionBillTexts.PaidTitle,
                Message = SessionBillTexts.PaymentMessage(session.Id, payment.Amount, payment.RequiresRefund),
                PayloadJson = JsonSerializer.Serialize(new { tableSessionId = session.Id, restaurantId = session.RestaurantId, paymentId = payment.Id, payment.IsTest })
            });
        }
    }
}
