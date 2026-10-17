using System.Data;
using System.Text.Json;
using AutoMapper;
using ECafe.Application.Common.Audit;
using ECafe.Application.Common.Exceptions;
using ECafe.Application.DTOs.Notification;
using ECafe.Application.DTOs.Payment;
using ECafe.Application.Mappings;
using ECafe.Application.Repositories.Reservation;
using ECafe.Application.Repositories.ReservationRefund;
using ECafe.Application.Repositories.Table;
using ECafe.Application.Repositories.User;
using ECafe.Application.Repositories.UserRestaurant;
using ECafe.Application.Repository;
using ECafe.Application.Services.AuditLog.Abstract;
using ECafe.Application.Services.FileAccess.Abstract;
using ECafe.Application.Services.Notification.Abstract;
using ECafe.Application.Services.Payments.Abstract;
using ECafe.Application.Services.Reservation.Concrete;
using ECafe.Application.Services.ReservationRefund.Abstract;
using ECafe.Application.Services.Workflow.Abstract;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using StatusType = ECafe.Domain.Enums.StatusType;
using ECafe.Domain.Exceptions;
using ECafe.Domain.Policies;
using ECafe.Domain.Workflow;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace ECafe.Application.Services.Payments.Concrete;

public sealed partial class ProviderPaymentManager(IHttpContextAccessor accessor, IMapper mapper, IConfiguration configuration,
    IReservationRepository reservations, IBaseRepository<Payment> payments, IReservationRefundRepository refunds,
    ITableRepository tables, IUserRepository users, IUserRestaurantRepository assignments,
    IApplicationDbTransactionFactory transactions, IWorkflowActionService workflow, IPaymentGateway gateway,
    PaymentRuntime runtime, IOptions<ReservationTimingOptions> timing, TimeProvider clock,
    INotificationService notifications, IReservationRefundNotifier refundNotifier, IAuditLogService audit,
    IFileAccessUrlService fileUrls) : BaseManager(accessor, mapper, configuration), IProviderPaymentService
{
    public PaymentCapabilities GetCapabilities() => runtime.GetCapabilities();

    public async Task<ReservationPaymentSummary> GetReservationAsync(int reservationId, CancellationToken cancellationToken)
    {
        var reservation = await FindReservationAsync(reservationId, false, cancellationToken);
        var customer = await RequireAccessAsync(reservation, false, cancellationToken);
        return BuildSummary(reservation, customer);
    }

    public async Task<ReservationPaymentSummary> AuthorizeDepositAsync(int restaurantId, int reservationId, CancellationToken cancellationToken)
    {
        runtime.EnsureEnabled();
        var snapshot = await FindReservationAsync(reservationId, false, cancellationToken);
        if (snapshot.RestaurantId != restaurantId) throw new NotFoundException(ErrorCode.ReservationNotFound);
        if (await RequireAccessAsync(snapshot, false, cancellationToken)) throw new ForbiddenException(ErrorCode.AccessDenied);
        await using var transaction = await transactions.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var reservation = await LockReservationAsync(snapshot, false, cancellationToken);
        var now = clock.GetUtcNow().UtcDateTime;
        if (reservation.StatusId == StatusIds.Reservation(ReservationStatus.PendingPayment) && reservation.HoldExpiresAt > now)
        {
            await transaction.CommitAsync(cancellationToken);
            return BuildSummary(reservation, false);
        }
        if (!CanAuthorize(reservation, now)) throw new BusinessRuleException(ErrorCode.PaymentTransitionNotAllowed);
        await workflow.EnsureCanExecuteAsync(WorkflowFlowCode.FromStatusType(StatusType.Reservation), reservation.StatusId,
            WorkflowActionCode.Reservation.SendPaymentInstruction, restaurantId, reservation.Id);
        if (await tables.GetReservationTableAvailabilityAsync(restaurantId, reservation.TableId,
                new DateTimeOffset(DateTime.SpecifyKind(reservation.ReservedAt, DateTimeKind.Utc)), reservation.Id) == null)
            throw new BusinessRuleException(ErrorCode.TableAlreadyReserved);
        var previous = reservation.StatusId;
        reservation.StatusId = StatusIds.Reservation(ReservationStatus.PendingPayment);
        reservation.RestaurantResponseExpiresAt = null;
        reservation.HoldExpiresAt = now.AddMinutes(reservation.Restaurant.PaymentHoldMinutes);
        reservation.StatusHistory.Add(new ReservationStatusHistory
        {
            FromStatusId = previous, ToStatusId = reservation.StatusId, ChangedByUserId = GetCurrentUserId(),
            ChangedAt = now, Reason = PaymentTexts.Authorized
        });
        await reservations.SaveChangesAsync();
        await NotifyCustomerAsync(reservation, NotificationType.ReservationPaymentInstructionSent, "Depoziti ödəyə bilərsiniz", PaymentTexts.Authorized);
        await audit.RecordRestaurantActionAsync(restaurantId, AuditActions.ProviderPaymentAuthorized,
            new { reservationId, amount = reservation.DepositAmount, isTest = gateway.IsTest }, AuditEntityTypes.Reservation, reservationId);
        await transaction.CommitAsync(cancellationToken);
        return BuildSummary(reservation, false);
    }

    public async Task<CheckoutPaymentResponse> CreateCheckoutAsync(int reservationId, string key, CancellationToken cancellationToken)
    {
        runtime.EnsureEnabled();
        runtime.EnsureSimulation(gateway.IsTest);
        if (!Guid.TryParseExact(key, "D", out var parsed)) throw new BadRequestException(ErrorCode.PaymentRequestKeyRequired);
        key = parsed.ToString("D");
        var snapshot = await FindReservationAsync(reservationId, false, cancellationToken);
        await RequireAccessAsync(snapshot, true, cancellationToken);
        await using var transaction = await transactions.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var reservation = await LockReservationAsync(snapshot, true, cancellationToken);
        var userId = GetCurrentUserId();
        var existing = await payments.Query(payment => payment.CreatedByUserId == userId && payment.CheckoutKey == key)
            .SingleOrDefaultAsync(cancellationToken);
        if (existing != null)
        {
            if (existing.ReservationId != reservationId || existing.RestaurantId != reservation.RestaurantId)
                throw new BusinessRuleException(ErrorCode.PaymentRequestKeyConflict);
            await transaction.CommitAsync(cancellationToken);
            return ProviderPaymentMapper.Map(existing);
        }
        var now = clock.GetUtcNow().UtcDateTime;
        if (!CanPay(reservation, now)) throw new BusinessRuleException(ErrorCode.PaymentTransitionNotAllowed);
        if (reservation.Payments.Any(payment => !payment.IsDeleted &&
                payment.PaymentStatusId == StatusIds.Build(StatusType.PaymentStatus, (int)PaymentStatus.Pending)))
            throw new BusinessRuleException(ErrorCode.PaymentRequestKeyConflict);
        var payment = new Payment
        {
            RestaurantId = reservation.RestaurantId, ReservationId = reservationId, Reservation = reservation,
            CreatedByUserId = userId, Amount = reservation.DepositAmount, CurrencyCode = "AZN", CheckoutKey = key,
            PaymentMethodId = StatusIds.Build(StatusType.PaymentMethod, (int)PaymentMethod.Online),
            PaymentStatusId = StatusIds.Build(StatusType.PaymentStatus, (int)PaymentStatus.Pending),
            ProviderName = gateway.Name, IsTest = gateway.IsTest, CheckoutExpiresAt = reservation.HoldExpiresAt
        };
        await payments.Add(payment);
        await payments.SaveChangesAsync();
        // Provayder adapteri Payment.Id əsasında sabit idempotency açarı istifadə etməlidir; browser açarı provayderə ötürülmür.
        await gateway.CreateCheckoutAsync(payment, cancellationToken);
        await payments.SaveChangesAsync();
        await audit.RecordRestaurantActionAsync(reservation.RestaurantId, AuditActions.ProviderCheckoutCreated,
            new { reservationId, paymentId = payment.Id, payment.Amount, payment.IsTest }, AuditEntityTypes.Reservation, reservationId);
        await transaction.CommitAsync(cancellationToken);
        return ProviderPaymentMapper.Map(payment);
    }

    public Task<CheckoutPaymentResponse> RefreshAsync(int paymentId, CancellationToken cancellationToken)
        => ReconcileAsync(paymentId, null, cancellationToken);

    public Task<CheckoutPaymentResponse> SimulateAsync(int paymentId, ProviderOperationStatus outcome, CancellationToken cancellationToken)
        => ReconcileAsync(paymentId, outcome, cancellationToken);

    /// <summary>Browserin uğur səhifəsinə etibar etmir; provayderdən yoxlanmış nəticəni masa kilidində bir dəfə tətbiq edir.</summary>
    private async Task<CheckoutPaymentResponse> ReconcileAsync(int paymentId, ProviderOperationStatus? outcome, CancellationToken cancellationToken)
    {
        runtime.EnsureEnabled();
        var snapshot = await payments.Query(payment => payment.Id == paymentId && payment.CreatedByUserId == GetCurrentUserId())
            .SingleOrDefaultAsync(cancellationToken) ?? throw new NotFoundException(ErrorCode.PaymentNotFound);
        var reservationSnapshot = await FindReservationAsync(snapshot.ReservationId ?? 0, false, cancellationToken);
        await RequireAccessAsync(reservationSnapshot, true, cancellationToken);
        await using var transaction = await transactions.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var reservation = await LockReservationAsync(reservationSnapshot, true, cancellationToken);
        var payment = reservation.Payments.SingleOrDefault(item => item.Id == paymentId && item.CreatedByUserId == GetCurrentUserId())
            ?? throw new NotFoundException(ErrorCode.PaymentNotFound);
        EnsureGatewayMatches(payment);
        if (outcome.HasValue)
        {
            runtime.EnsureSimulation(payment.IsTest);
            if (!Enum.IsDefined(outcome.Value)) throw new BadRequestException(ErrorCode.PaymentTransitionNotAllowed);
            if (payment.PaymentStatusId == StatusIds.Build(StatusType.PaymentStatus, (int)PaymentStatus.Pending))
                payment.ProviderStatus = outcome.Value;
        }
        var result = await gateway.VerifyPaymentAsync(payment, cancellationToken);
        ProviderPaymentPolicy.ValidateResult(payment, result.Reference, result.Amount, result.CurrencyCode, result.IsTest);
        if (ProviderPaymentPolicy.Apply(payment, reservation, result.Status, clock.GetUtcNow().UtcDateTime, timing.Value.CancellationGraceMinutes))
        {
            await payments.SaveChangesAsync();
            if (payment.RequiresRefund) await CreateRefundAsync(reservation, payment, PaymentTexts.LatePaymentRefund, cancellationToken);
            if (result.Status == ProviderOperationStatus.Succeeded)
            {
                await NotifyCustomerAsync(reservation, payment.RequiresRefund ? NotificationType.ReservationRefundRequested : NotificationType.ReservationConfirmed,
                    payment.RequiresRefund ? "Ödəniş geri qaytarılmalıdır" : "Rezervasiya təsdiqləndi",
                    payment.RequiresRefund ? PaymentTexts.LatePaymentRefund : PaymentTexts.Confirmed);
                if (!payment.RequiresRefund) await NotifyRestaurantPaidAsync(reservation, cancellationToken);
            }
            await audit.RecordRestaurantActionAsync(reservation.RestaurantId, AuditActions.ProviderPaymentUpdated,
                new { paymentId, payment.PaymentStatusId, payment.Amount, payment.IsTest, payment.RequiresRefund }, AuditEntityTypes.Reservation, reservation.Id);
        }
        await transaction.CommitAsync(cancellationToken);
        return ProviderPaymentMapper.Map(payment);
    }

    /// <summary>Rezervasiyanın ödəniş üçün lazım olan vəziyyətini yükləyir; kart və çek mətnlərini sorğuya daxil etmir.</summary>
    private async Task<Domain.Entities.Reservation> FindReservationAsync(int id, bool tracked, CancellationToken cancellationToken)
    {
        return await reservations.GetForProviderPaymentAsync(id, tracked, cancellationToken)
            ?? throw new NotFoundException(ErrorCode.ReservationNotFound);
    }

    /// <summary>Masa və müştəri kilidlərindən sonra aidiyyət, cari rol və dəyişmiş masa ID-sini yenidən yoxlayır.</summary>
    private async Task<Domain.Entities.Reservation> LockReservationAsync(Domain.Entities.Reservation snapshot, bool customerOnly, CancellationToken cancellationToken)
    {
        await tables.AcquireReservationLockAsync(snapshot.RestaurantId, snapshot.TableId, cancellationToken);
        await reservations.AcquireCustomerReservationLockAsync(snapshot.RestaurantId, snapshot.CustomerUserId, cancellationToken);
        var current = await FindReservationAsync(snapshot.Id, true, cancellationToken);
        var customer = await RequireAccessAsync(current, customerOnly, cancellationToken);
        if (!customerOnly && customer) throw new ForbiddenException(ErrorCode.AccessDenied);
        if (current.TableId != snapshot.TableId) throw new BusinessRuleException(ErrorCode.ReservationTableChangedRetry);
        return current;
    }

    /// <summary>Tokenlə yanaşı hesabın aktivliyini və restoran üzrə canlı menecer/sahibkar təyinatını yoxlayır; yad müştəriyə 404 verir.</summary>
    private async Task<bool> RequireAccessAsync(Domain.Entities.Reservation reservation, bool customerOnly, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (!await users.Query(user => user.Id == userId && user.IsActive).AnyAsync(cancellationToken))
            throw new ForbiddenException(ErrorCode.AccessDenied);
        if (GetCurrentRoleId() == (int)RoleCode.Customer)
        {
            if (reservation.CustomerUserId != userId) throw new NotFoundException(ErrorCode.ReservationNotFound);
            return true;
        }
        if (customerOnly) throw new ForbiddenException(ErrorCode.AccessDenied);
        EnsureCurrentUserCanAccessRestaurant(reservation.RestaurantId);
        if (await assignments.GetActiveRoleIdAsync(userId, reservation.RestaurantId) is not ((int)RoleCode.Owner) and not ((int)RoleCode.Manager))
            throw new ForbiddenException(ErrorCode.AccessDenied);
        return false;
    }

    /// <summary>UI üçün rol, cari müddət və server rejiminə uyğun əməliyyat işarələrini hesablayır; icazə serverdə yenidən yoxlanır.</summary>
    private ReservationPaymentSummary BuildSummary(Domain.Entities.Reservation reservation, bool customer)
    {
        var enabled = runtime.GetCapabilities();
        var latest = reservation.Payments.Where(payment => !payment.IsDeleted && payment.ProviderName != null)
            .OrderByDescending(payment => payment.Id).FirstOrDefault();
        return new(reservation.Id, enabled.Enabled, enabled.IsTest, enabled.Enabled && !customer && CanAuthorize(reservation, clock.GetUtcNow().UtcDateTime),
            enabled.Enabled && customer && CanPay(reservation, clock.GetUtcNow().UtcDateTime), latest == null ? null : ProviderPaymentMapper.Map(latest));
    }

    /// <summary>Restoran cavab müddəti və depozit məbləği üzrə əvvəlki qaydanı saxlayır.</summary>
    private static bool CanAuthorize(Domain.Entities.Reservation reservation, DateTime now)
        => reservation.Restaurant.IsActive && reservation.DepositAmount > 0 && reservation.ReservedAt > now &&
            reservation.StatusId == StatusIds.Reservation(ReservationStatus.AwaitingPaymentInstruction) && reservation.RestaurantResponseExpiresAt > now;

    /// <summary>Yalnız restoranın açdığı ödəniş müddətində, depozit hələ ödənməyibsə checkout yaratmağa icazə verir.</summary>
    private static bool CanPay(Domain.Entities.Reservation reservation, DateTime now)
        => reservation.Restaurant.IsActive && reservation.DepositAmount > 0 && reservation.StatusId == StatusIds.Reservation(ReservationStatus.PendingPayment) &&
            reservation.ReservedAt > now && reservation.HoldExpiresAt > now && !ReservationDepositPolicy.HasConfirmedDeposit(reservation);

    /// <summary>Test adapterinin köhnə və ya başqa provayderin əməliyyatını təsdiqləməsinin qarşısını alır.</summary>
    private void EnsureGatewayMatches(Payment payment)
    {
        runtime.EnsureSimulation(payment.IsTest);
        if (payment.ProviderName != gateway.Name || payment.IsTest != gateway.IsTest)
            throw new BusinessRuleException(ErrorCode.PaymentProviderMismatch);
    }

    /// <summary>Həssas məlumat olmadan mövcud sayt/e-mail/push outbox yoluna müştəri bildirişi yazır.</summary>
    private Task NotifyCustomerAsync(Domain.Entities.Reservation reservation, NotificationType type, string title, string message)
        => notifications.CreateAsync(new CreateNotificationRequest
        {
            UserId = reservation.CustomerUserId, RestaurantId = reservation.RestaurantId, TypeId = (int)type,
            ChannelId = (int)NotificationChannel.InApp, Title = gateway.IsTest ? $"[TEST] {title}" : title,
            Message = gateway.IsTest ? $"{PaymentTexts.TestNotice} {message}" : message,
            RelatedEntityType = AuditEntityTypes.Reservation, RelatedEntityId = reservation.Id,
            PayloadJson = JsonSerializer.Serialize(new { reservationId = reservation.Id, restaurantId = reservation.RestaurantId, isTest = gateway.IsTest })
        });

    /// <summary>Depozitin təsdiqini yalnız aidiyyəti restoranın aktiv menecer və sahibkarlarına mövcud outbox ilə çatdırır.</summary>
    private async Task NotifyRestaurantPaidAsync(Domain.Entities.Reservation reservation, CancellationToken cancellationToken)
    {
        var staff = await assignments.GetActiveByRestaurantAndRolesAsync(reservation.RestaurantId, [(int)RoleCode.Owner, (int)RoleCode.Manager]);
        foreach (var userId in staff.Select(item => item.UserId).Distinct())
        {
            cancellationToken.ThrowIfCancellationRequested();
            await notifications.CreateAsync(new CreateNotificationRequest
            {
                UserId = userId, RestaurantId = reservation.RestaurantId, TypeId = (int)NotificationType.ReservationConfirmed,
                ChannelId = (int)NotificationChannel.InApp, Title = "[TEST] Depozit ödənildi",
                Message = $"{PaymentTexts.TestNotice} Rezervasiya #{reservation.Id}: {PaymentTexts.Confirmed}",
                RelatedEntityType = AuditEntityTypes.Reservation, RelatedEntityId = reservation.Id,
                PayloadJson = JsonSerializer.Serialize(new { reservationId = reservation.Id, restaurantId = reservation.RestaurantId, isTest = gateway.IsTest })
            });
        }
    }
}
