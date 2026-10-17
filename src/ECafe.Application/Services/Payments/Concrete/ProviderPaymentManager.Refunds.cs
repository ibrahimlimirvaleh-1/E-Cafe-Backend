using System.Data;
using ECafe.Application.Common.Audit;
using ECafe.Application.Common.Exceptions;
using ECafe.Application.DTOs.Reservation;
using ECafe.Application.Mappings;
using ECafe.Domain.Entities;
using RefundEntity = ECafe.Domain.Entities.ReservationRefund;
using ECafe.Domain.Enums;
using StatusType = ECafe.Domain.Enums.StatusType;
using ECafe.Domain.Exceptions;
using ECafe.Domain.Policies;
using ECafe.Domain.Workflow;
using Microsoft.EntityFrameworkCore;

namespace ECafe.Application.Services.Payments.Concrete;

public sealed partial class ProviderPaymentManager
{
    public async Task<ReservationRefundResponse> RequestRefundAsync(int reservationId, CancellationToken cancellationToken)
    {
        var snapshot = await FindReservationAsync(reservationId, false, cancellationToken);
        await RequireAccessAsync(snapshot, true, cancellationToken);
        await using var transaction = await transactions.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var reservation = await LockReservationAsync(snapshot, true, cancellationToken);
        var existing = await refunds.GetByReservationForCustomerAsync(reservationId, GetCurrentUserId(), cancellationToken);
        if (existing != null)
        {
            await transaction.CommitAsync(cancellationToken);
            return ReservationRefundResponseMapper.Map(existing, fileUrls);
        }
        if (reservation.RefundEligible != true) throw new BusinessRuleException(ErrorCode.ReservationRefundNotEligible);
        await workflow.EnsureCanExecuteAsync(WorkflowFlowCode.FromStatusType(StatusType.Reservation), reservation.StatusId,
            WorkflowActionCode.Reservation.RequestRefund, reservation.RestaurantId, reservation.Id);
        var payment = ReservationDepositPolicy.GetConfirmedPayment(reservation);
        if (payment != null)
        {
            runtime.EnsureEnabled();
            EnsureGatewayMatches(payment);
            if (payment.RefundedAt != null) throw new BusinessRuleException(ErrorCode.ReservationRefundAlreadyRequested);
        }
        var refund = await CreateRefundAsync(reservation, payment, ReservationRefundRequestMapper.GetEligibilityReason(reservation), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ReservationRefundResponseMapper.Map(refund, fileUrls);
    }

    public Task<ReservationRefundResponse> RefreshRefundAsync(int refundId, CancellationToken cancellationToken)
        => ReconcileRefundAsync(refundId, null, cancellationToken);

    public Task<ReservationRefundResponse> SimulateRefundAsync(int refundId, ProviderOperationStatus outcome, CancellationToken cancellationToken)
        => ReconcileRefundAsync(refundId, outcome, cancellationToken);

    /// <summary>Testdə nəticəni simulyasiya edir, normal yoxlamada isə provayderdən oxuyur; təsdiq, tarix və bildiriş bir dəfə saxlanılır.</summary>
    private async Task<ReservationRefundResponse> ReconcileRefundAsync(int refundId, ProviderOperationStatus? outcome, CancellationToken cancellationToken)
    {
        runtime.EnsureEnabled();
        var snapshot = await refunds.GetByIdForCustomerSnapshotAsync(refundId, GetCurrentUserId(), cancellationToken)
            ?? throw new NotFoundException(ErrorCode.ReservationRefundNotFound);
        var reservationSnapshot = await FindReservationAsync(snapshot.ReservationId, false, cancellationToken);
        await RequireAccessAsync(reservationSnapshot, true, cancellationToken);
        await using var transaction = await transactions.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var reservation = await LockReservationAsync(reservationSnapshot, true, cancellationToken);
        var refund = await refunds.GetByIdForCustomerForUpdateAsync(refundId, GetCurrentUserId(), cancellationToken)
            ?? throw new NotFoundException(ErrorCode.ReservationRefundNotFound);
        var payment = reservation.Payments.SingleOrDefault(item => item.Id == refund.SourcePaymentId)
            ?? throw new BusinessRuleException(ErrorCode.PaymentSimulationUnavailable);
        EnsureGatewayMatches(payment);
        if (outcome.HasValue)
        {
            runtime.EnsureSimulation(payment.IsTest);
            if (!Enum.IsDefined(outcome.Value) || outcome == ProviderOperationStatus.Cancelled)
                throw new BadRequestException(ErrorCode.PaymentTransitionNotAllowed);
            if (refund.StatusId == StatusIds.Refund(RefundStatus.Processing)) refund.ProviderStatus = outcome.Value;
        }
        var result = await gateway.VerifyRefundAsync(payment, refund, cancellationToken);
        if (ProviderRefundPolicy.Apply(refund, payment, result.Status, result.Reference, result.Amount,
                result.CurrencyCode, result.IsTest, clock.GetUtcNow().UtcDateTime))
        {
            await refunds.SaveChangesAsync();
            var message = result.Status == ProviderOperationStatus.Succeeded ? PaymentTexts.RefundCompleted : PaymentTexts.RefundFailed;
            var notificationType = result.Status == ProviderOperationStatus.Succeeded ? NotificationType.ReservationRefundTransferConfirmed : NotificationType.ReservationRefundRequested;
            await NotifyCustomerAsync(reservation, notificationType, "Geri ödəniş yeniləndi", message);
            await refundNotifier.NotifyRestaurantAsync(reservation, refund, notificationType,
                "[TEST] Geri ödəniş yeniləndi", $"{PaymentTexts.TestNotice} {message}");
            await audit.RecordRestaurantActionAsync(reservation.RestaurantId, AuditActions.ProviderRefundUpdated,
                new { refundId, paymentId = payment.Id, refund.StatusId, refund.Amount, payment.IsTest }, AuditEntityTypes.ReservationRefund, refund.Id);
        }
        await transaction.CommitAsync(cancellationToken);
        return ReservationRefundResponseMapper.Map(refund, fileUrls);
    }

    /// <summary>Bir rezervasiya üçün tək refund yaradır; köhnə çek ödənişi yalnız yoxlamaya düşür, provayderdə yeni rekvizit toplanmır.</summary>
    private async Task<RefundEntity> CreateRefundAsync(Domain.Entities.Reservation reservation, Payment? payment,
        string reason, CancellationToken cancellationToken)
    {
        var existing = await refunds.QueryTracked(item => item.ReservationId == reservation.Id)
            .Include(item => item.SourcePayment).Include(item => item.StatusHistory).SingleOrDefaultAsync(cancellationToken);
        if (existing != null)
        {
            if (existing.SourcePaymentId != payment?.Id) throw new BusinessRuleException(ErrorCode.PaymentProviderMismatch);
            return existing;
        }
        var proof = payment == null ? reservation.PaymentProofs.Where(item => !item.IsDeleted &&
                item.StatusId == StatusIds.Reservation(ReservationStatus.Confirmed))
            .OrderByDescending(item => item.SubmittedAt).ThenByDescending(item => item.Id).FirstOrDefault() : null;
        if (payment == null && proof == null) throw new BusinessRuleException(ErrorCode.ConfirmedReservationDepositNotFound);
        var now = clock.GetUtcNow().UtcDateTime;
        var status = StatusIds.Refund(payment == null ? RefundStatus.Requested : RefundStatus.Processing);
        var refund = new RefundEntity
        {
            ReservationId = reservation.Id, Reservation = reservation, SourcePaymentId = payment?.Id, SourcePayment = payment,
            SourcePaymentProofId = proof?.Id, ProviderName = payment?.ProviderName, Amount = payment?.Amount ?? proof!.Amount,
            CurrencyCode = payment?.CurrencyCode ?? "AZN", RequestedAt = now, ApprovedAt = now, InitiatedByUserId = GetCurrentUserId(),
            StatusId = status, EligibilityReason = payment == null ? PaymentTexts.ManualReview : reason,
            CancellationReasonSnapshot = reservation.CancelReason,
            StatusHistory = [new ReservationRefundStatusHistory
            {
                ToStatusId = status, ChangedAt = now, ChangedByUserId = GetCurrentUserId(),
                Reason = payment == null ? PaymentTexts.ManualReview : PaymentTexts.RefundPending
            }]
        };
        await refunds.Add(refund);
        await refunds.SaveChangesAsync();
        if (payment != null)
        {
            EnsureGatewayMatches(payment);
            await gateway.CreateRefundAsync(payment, refund, cancellationToken);
            await refunds.SaveChangesAsync();
        }
        await refundNotifier.NotifyRestaurantAsync(reservation, refund, NotificationType.ReservationRefundRequested,
            payment?.IsTest == true ? "[TEST] Geri ödəniş sorğusu" : "Geri ödəniş sorğusu",
            payment == null ? PaymentTexts.ManualReview : $"{PaymentTexts.TestNotice} {PaymentTexts.RefundPending}");
        await audit.RecordRestaurantActionAsync(reservation.RestaurantId, AuditActions.ReservationRefundRequested,
            new { reservationId = reservation.Id, refundId = refund.Id, sourcePaymentId = payment?.Id, refund.Amount,
                isTest = payment?.IsTest == true, requiresManualReview = payment == null }, AuditEntityTypes.ReservationRefund, refund.Id);
        return refund;
    }
}
