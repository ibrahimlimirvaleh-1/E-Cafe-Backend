using System.Data;
using System.Text.Json;
using AutoMapper;
using ECafe.Application.Common.Audit;
using ECafe.Application.Common.Exceptions;
using ECafe.Application.DTOs.Notification;
using ECafe.Application.DTOs.Reservation;
using ECafe.Application.Repositories.Reservation;
using ECafe.Application.Repositories.Restaurant;
using ECafe.Application.Repositories.Table;
using ECafe.Application.Repositories.UserRestaurant;
using ECafe.Application.Repository;
using ECafe.Application.Services.AuditLog.Abstract;
using ECafe.Application.Services.Notification.Abstract;
using ECafe.Application.Services.Reservation.Abstract;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using ECafe.Domain.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using ReservationEntity = ECafe.Domain.Entities.Reservation;

namespace ECafe.Application.Services.Reservation.Concrete;

public sealed class ReservationArrivalManager(
    IHttpContextAccessor httpContextAccessor, IMapper mapper, IConfiguration configuration,
    IReservationRepository reservations, ITableRepository tables, IRestaurantRepository restaurants,
    IUserRestaurantRepository assignments, IApplicationDbTransactionFactory transactions,
    INotificationService notifications, IAuditLogService audit,
    IOptions<ReservationTimingOptions> timingOptions, TimeProvider clock)
    : BaseManager(httpContextAccessor, mapper, configuration), IReservationArrivalService
{
    private readonly ReservationTimingOptions _options = timingOptions.Value;

    public async Task<ReservationArrivalOptionsResponse> GetOptionsAsync(int reservationId, CancellationToken cancellationToken)
    {
        var reservation = await reservations.GetForArrivalAdjustmentAsync(reservationId, GetCurrentUserId(), false, cancellationToken)
            ?? throw new NotFoundException(ErrorCode.ReservationNotFound);
        var now = clock.GetUtcNow().UtcDateTime;
        var next = await tables.GetNextReservationAtAsync(reservation.RestaurantId, reservation.TableId,
            reservation.Id, reservation.ReservedAt, cancellationToken);
        var window = ReservationArrivalPolicy.GetWindow(reservation, next, _options);
        var canRequest = window != null && ReservationArrivalPolicy.CanRequest(reservation, now) &&
                         !await restaurants.HasPendingScheduleChangeAsync(reservation.RestaurantId, cancellationToken) &&
                         !await tables.HasOpenTableSessionAsync(reservation.RestaurantId, reservation.TableId) &&
                         await restaurants.HasRestaurantActiveContractAsync(reservation.RestaurantId);
        var choices = canRequest
            ? ReservationArrivalPolicy.GetChoices(reservation, now, window!, _options)
            : Array.Empty<DateTimeOffset>();
        var adjustment = reservation.ArrivalAdjustment;
        var offer = adjustment != null && (adjustment.AcceptedAt.HasValue ||
            reservation.StatusId == StatusIds.Reservation(ReservationStatus.Confirmed) && adjustment.DecisionExpiresAt > now)
            ? MapOffer(reservation, adjustment, now)
            : null;
        return new(canRequest && choices.Count > 0, choices, offer, reservation.Restaurant.TimeZone);
    }

    public async Task<ReservationArrivalOfferResponse> OfferAsync(int reservationId, DateTimeOffset arrivalAt,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var snapshot = await reservations.GetForArrivalAdjustmentAsync(reservationId, userId, false, cancellationToken)
            ?? throw new NotFoundException(ErrorCode.ReservationNotFound);
        await using var transaction = await transactions.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        await restaurants.AcquireScheduleLockAsync(snapshot.RestaurantId, cancellationToken);
        await tables.AcquireReservationLockAsync(snapshot.RestaurantId, snapshot.TableId, cancellationToken);
        var reservation = await reservations.GetForArrivalAdjustmentAsync(reservationId, userId, true, cancellationToken)
            ?? throw new NotFoundException(ErrorCode.ReservationNotFound);
        var now = clock.GetUtcNow().UtcDateTime;
        EnsureCanRequest(reservation, now);
        if (await restaurants.HasPendingScheduleChangeAsync(reservation.RestaurantId, cancellationToken))
            throw new BusinessRuleException(ErrorCode.ReservationArrivalNotAvailable);
        if (await tables.HasOpenTableSessionAsync(reservation.RestaurantId, reservation.TableId) ||
            !await restaurants.HasRestaurantActiveContractAsync(reservation.RestaurantId))
            throw new BusinessRuleException(ErrorCode.ReservationArrivalNotAvailable);

        var next = await tables.GetNextReservationAtAsync(reservation.RestaurantId, reservation.TableId,
            reservation.Id, reservation.ReservedAt, cancellationToken);
        var window = ReservationArrivalPolicy.GetWindow(reservation, next, _options)
            ?? throw new BusinessRuleException(ErrorCode.ReservationArrivalNotAvailable);
        var requestedAt = arrivalAt.UtcDateTime;
        if (requestedAt <= now || requestedAt <= reservation.ReservedAt || requestedAt >= window.MaximumDeadlineAt)
            throw new BusinessRuleException(ErrorCode.ReservationArrivalTimeInvalid);
        var decisionDeadline = ReservationArrivalPolicy.GetDecisionDeadline(reservation, now, window, _options);
        if (decisionDeadline <= now)
            throw new BusinessRuleException(ErrorCode.ReservationArrivalOfferExpired);

        var adjustment = reservation.ArrivalAdjustment ?? new ReservationArrivalAdjustment
        {
            ReservationId = reservation.Id,
            OriginalNoShowDeadlineAt = reservation.NoShowDeadlineAt,
            MaximumNoShowDeadlineAt = window.MaximumDeadlineAt
        };
        // Refreshing an offer never moves the absolute consent hold past the original deadline plus its short buffer.
        adjustment.RequestedArrivalAt = requestedAt;
        adjustment.ProposedNoShowDeadlineAt = ReservationArrivalPolicy.GetProposedDeadline(reservation, requestedAt, window);
        adjustment.MustVacateAt = window.MustVacateAt;
        adjustment.DecisionExpiresAt = decisionDeadline;
        adjustment.ConsentToken = Guid.NewGuid();
        reservation.ArrivalAdjustment = adjustment;
        await reservations.SaveChangesAsync();
        await transaction.CommitAsync(cancellationToken);
        return MapOffer(reservation, adjustment, now);
    }

    public async Task<ReservationArrivalOfferResponse> AcceptAsync(int reservationId, Guid consentToken,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var snapshot = await reservations.GetForArrivalAdjustmentAsync(reservationId, userId, false, cancellationToken)
            ?? throw new NotFoundException(ErrorCode.ReservationNotFound);
        await using var transaction = await transactions.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        await restaurants.AcquireScheduleLockAsync(snapshot.RestaurantId, cancellationToken);
        await tables.AcquireReservationLockAsync(snapshot.RestaurantId, snapshot.TableId, cancellationToken);
        var reservation = await reservations.GetForArrivalAdjustmentAsync(reservationId, userId, true, cancellationToken)
            ?? throw new NotFoundException(ErrorCode.ReservationNotFound);
        var now = clock.GetUtcNow().UtcDateTime;
        var adjustment = reservation.ArrivalAdjustment;
        if (adjustment == null || consentToken == Guid.Empty || adjustment.ConsentToken != consentToken)
            throw new BusinessRuleException(ErrorCode.ReservationArrivalTermsChanged);
        if (adjustment.AcceptedAt.HasValue)
        {
            await transaction.CommitAsync(cancellationToken);
            return MapOffer(reservation, adjustment, now);
        }
        EnsureCanRequest(reservation, now);
        if (await restaurants.HasPendingScheduleChangeAsync(reservation.RestaurantId, cancellationToken))
            throw new BusinessRuleException(ErrorCode.ReservationArrivalNotAvailable);
        if (adjustment.DecisionExpiresAt <= now)
            throw new BusinessRuleException(ErrorCode.ReservationArrivalOfferExpired);
        if (await tables.HasOpenTableSessionAsync(reservation.RestaurantId, reservation.TableId) ||
            !await restaurants.HasRestaurantActiveContractAsync(reservation.RestaurantId))
            throw new BusinessRuleException(ErrorCode.ReservationArrivalNotAvailable);
        var next = await tables.GetNextReservationAtAsync(reservation.RestaurantId, reservation.TableId,
            reservation.Id, reservation.ReservedAt, cancellationToken);
        var window = ReservationArrivalPolicy.GetWindow(reservation, next, _options);
        if (window == null || adjustment.RequestedArrivalAt >= window.MaximumDeadlineAt ||
            adjustment.ProposedNoShowDeadlineAt <= now ||
            adjustment.MustVacateAt != window.MustVacateAt ||
            adjustment.ProposedNoShowDeadlineAt != ReservationArrivalPolicy.GetProposedDeadline(reservation, adjustment.RequestedArrivalAt, window))
            throw new BusinessRuleException(ErrorCode.ReservationArrivalTermsChanged);

        adjustment.AcceptedAt = now;
        reservation.NoShowDeadlineAt = adjustment.ProposedNoShowDeadlineAt;
        reservation.MustVacateAt = adjustment.MustVacateAt;
        reservation.StatusHistory.Add(new ReservationStatusHistory
        {
            FromStatusId = reservation.StatusId,
            ToStatusId = reservation.StatusId,
            ChangedByUserId = userId,
            ChangedAt = now,
            Reason = "Müştəri yeni gəliş və masanın təhvil şərtlərini qəbul etdi."
        });
        await reservations.SaveChangesAsync();

        var managers = await assignments.GetActiveByRestaurantAndRolesAsync(reservation.RestaurantId,
            [(int)RoleCode.Manager, (int)RoleCode.Owner]);
        foreach (var recipient in managers.Select(x => x.UserId).Append(userId).Distinct())
        {
            await notifications.CreateAsync(new CreateNotificationRequest
            {
                UserId = recipient,
                RestaurantId = reservation.RestaurantId,
                Title = "Gəliş vaxtı yeniləndi",
                Message = $"Rezervasiya #{reservation.Id} üzrə gecikmə təsdiqləndi. Yeni vaxtlar rezervasiya detalında göstərilir.",
                TypeId = (int)NotificationType.ReservationArrivalAdjusted,
                ChannelId = (int)NotificationChannel.InApp,
                RelatedEntityType = AuditEntityTypes.Reservation,
                RelatedEntityId = reservation.Id,
                PayloadJson = JsonSerializer.Serialize(new { reservationId = reservation.Id, restaurantId = reservation.RestaurantId })
            });
        }
        await audit.RecordRestaurantActionAsync(reservation.RestaurantId, AuditActions.ReservationArrivalAdjusted,
            new { reservation.Id, adjustment.RequestedArrivalAt, reservation.NoShowDeadlineAt, reservation.MustVacateAt },
            AuditEntityTypes.Reservation, reservation.Id);
        await transaction.CommitAsync(cancellationToken);
        return MapOffer(reservation, adjustment, now);
    }

    private static void EnsureCanRequest(ReservationEntity reservation, DateTime now)
    {
        if (reservation.ArrivalAdjustment?.AcceptedAt != null)
            throw new BusinessRuleException(ErrorCode.ReservationArrivalAlreadyAdjusted);
        if (!ReservationArrivalPolicy.CanRequest(reservation, now))
            throw new BusinessRuleException(ErrorCode.ReservationArrivalNotAvailable);
    }

    private static ReservationArrivalOfferResponse MapOffer(ReservationEntity reservation,
        ReservationArrivalAdjustment adjustment, DateTime now)
        => new(adjustment.ConsentToken, ToOffset(adjustment.RequestedArrivalAt), ToOffset(adjustment.ProposedNoShowDeadlineAt),
            adjustment.MustVacateAt.HasValue ? ToOffset(adjustment.MustVacateAt.Value) : null,
            ToOffset(adjustment.DecisionExpiresAt), adjustment.AcceptedAt.HasValue,
            reservation.PaymentProofs.Any(p => p.StatusId == StatusIds.Reservation(ReservationStatus.Confirmed)),
            ReservationCancellationRules.CanRefund(reservation, now, true), reservation.Restaurant.TimeZone);

    private static DateTimeOffset ToOffset(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
}
