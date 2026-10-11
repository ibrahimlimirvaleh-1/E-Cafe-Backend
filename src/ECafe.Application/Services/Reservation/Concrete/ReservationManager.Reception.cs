using System.Data;
using System.Text.Json;
using ECafe.Application.Common.Audit;
using ECafe.Application.Common.Exceptions;
using ECafe.Application.DTOs.Notification;
using ECafe.Application.DTOs.Reservation;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using ReservationEntity = ECafe.Domain.Entities.Reservation;

namespace ECafe.Application.Services.Reservation.Concrete;

public sealed partial class ReservationManager
{
    private static readonly int[] ReceptionStaffRoleIds =
        [(int)RoleCode.Waiter, (int)RoleCode.Manager, (int)RoleCode.Owner];

    public async Task<ReservationActionResponse> RecordWaitingDepartureAsync(
        int restaurantId, int reservationId, ReservationDepartureRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRestaurantId(restaurantId);
        ValidateReservationId(reservationId);
        await EnsureRestaurantServiceRoleAsync(restaurantId, RoleCode.Manager, RoleCode.Owner);
        var reason = request.Reason?.Trim();
        if (!Enum.IsDefined(request.ReasonCode) || string.IsNullOrWhiteSpace(reason) || reason.Length > 500)
            throw new BadRequestException(ErrorCode.ReservationDepartureReasonRequired);

        return await CancelReservationCoreAsync(restaurantId, reservationId, GetCurrentUserId(),
            reason, isCustomer: false, cancellationToken, request.ReasonCode);
    }

    public async Task<ReservationActionResponse> ChangeWaitingTableAsync(
        int restaurantId, int reservationId, ReservationTableChangeRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRestaurantId(restaurantId);
        ValidateReservationId(reservationId);
        await EnsureRestaurantServiceRoleAsync(restaurantId, RoleCode.Manager, RoleCode.Owner);
        if (!request.CustomerConsent)
            throw new BadRequestException(ErrorCode.ReservationTableChangeConsentRequired);
        if (request.TableId <= 0)
            throw new BadRequestException(ErrorCode.ReservationTableChangeTermsConflict);

        await using var transaction = await _transactionFactory.BeginTransactionAsync(
            IsolationLevel.ReadCommitted, cancellationToken);
        var snapshot = await _reservationRepository.GetByIdForRestaurantSnapshotAsync(
            reservationId, restaurantId, cancellationToken)
            ?? throw new NotFoundException(ErrorCode.ReservationNotFound);

        // Hər keçid masaları eyni ardıcıllıqla kilidləyir ki, qarşılıqlı masa dəyişməsi deadlock yaratmasın.
        foreach (var tableId in new[] { snapshot.TableId, request.TableId }.Distinct().Order())
            await _tableRepository.AcquireReservationLockAsync(restaurantId, tableId, cancellationToken);

        var reservation = await _reservationRepository.GetByIdForRestaurantAsync(
            reservationId, restaurantId, cancellationToken)
            ?? throw new NotFoundException(ErrorCode.ReservationNotFound);
        EnsureSnapshotTableUnchanged(snapshot, reservation);
        EnsureWaitingGuest(reservation);

        if (reservation.TableId == request.TableId)
        {
            await transaction.CommitAsync(cancellationToken);
            return BuildActionResponse(reservation.Id, reservation.StatusId, ReservationStatus.Confirmed,
                ReservationReceptionTexts.TableChangedMessage);
        }

        var nowUtc = DateTime.UtcNow;
        var target = await EnsureSeatingTableAvailableAsync(reservation, request.TableId, nowUtc);
        var previousTableId = reservation.TableId;
        reservation.TableId = target.Id;
        reservation.Table = target;
        reservation.StatusHistory.Add(new ReservationStatusHistory
        {
            FromStatusId = reservation.StatusId,
            ToStatusId = reservation.StatusId,
            ChangedAt = nowUtc,
            ChangedByUserId = GetCurrentUserId(),
            Reason = $"{ReservationReceptionTexts.TableChangedTitle}: {snapshot.Table?.Name ?? previousTableId.ToString()} → {target.Name ?? target.TableNo.ToString()}."
        });
        await _reservationRepository.SaveChangesAsync();
        await NotifyReservationCustomerAsync(restaurantId, reservation, NotificationType.ReservationTableChanged,
            ReservationReceptionTexts.TableChangedTitle, ReservationReceptionTexts.TableChangedMessage);
        await NotifyReceptionStaffAsync(reservation, ReceptionStaffRoleIds, NotificationType.ReservationTableChanged,
            ReservationReceptionTexts.TableChangedTitle, ReservationReceptionTexts.ArrivedMessage(reservation.Id, target.Name));
        await _auditLogService.RecordRestaurantActionAsync(restaurantId, AuditActions.ReservationTableChanged,
            new { reservationId, previousTableId, tableId = target.Id, customerConsent = true },
            AuditEntityTypes.Reservation, reservationId);
        await transaction.CommitAsync(cancellationToken);
        return BuildActionResponse(reservation.Id, reservation.StatusId, ReservationStatus.Confirmed,
            ReservationReceptionTexts.TableChangedMessage);
    }

    public async Task<int> EscalateWaitingReservationsAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        if (batchSize <= 0)
            return 0;
        var candidates = await _reservationRepository.GetWaitingEscalationCandidatesAsync(
            DateTime.UtcNow, _timingOptions.WaitingEscalationMinutes, batchSize, cancellationToken);
        var count = 0;
        foreach (var candidate in candidates)
        {
            await using var transaction = await _transactionFactory.BeginTransactionAsync(
                IsolationLevel.ReadCommitted, cancellationToken);
            var reservation = await LoadLockedRestaurantReservationAsync(
                candidate.RestaurantId, candidate.ReservationId, cancellationToken);
            var nowUtc = DateTime.UtcNow;
            var waiting = ReservationWaitingPolicy.GetInfo(reservation, nowUtc, _timingOptions.WaitingEscalationMinutes);
            if (waiting is not { IsOverdue: true } || reservation.WaitingEscalatedAt.HasValue)
            {
                await transaction.CommitAsync(cancellationToken);
                continue;
            }

            var notified = await NotifyReceptionStaffAsync(reservation, ReservationManagerRoleIds,
                NotificationType.ReservationWaitingEscalated, ReservationReceptionTexts.WaitingTitle,
                ReservationReceptionTexts.WaitingMessage(reservation.Id, waiting.ServiceDelayMinutes));
            if (notified > 0)
            {
                reservation.WaitingEscalatedAt = nowUtc;
                await _reservationRepository.SaveChangesAsync();
                count++;
            }
            await transaction.CommitAsync(cancellationToken);
        }
        return count;
    }

    /// <summary>Gəlişi yalnız rezervasiyanın aid olduğu iş intervalında qəbul edir; başqa günün gəlişi kimi qeydə alınmasını önləyir.</summary>
    private async Task EnsureReceptionIntervalAsync(int restaurantId, int reservationId,
        DateTime nowUtc, CancellationToken cancellationToken)
    {
        if (!await _reservationRepository.IsWithinReservationServiceIntervalAsync(
                restaurantId, reservationId, nowUtc, cancellationToken))
            throw new BusinessRuleException(ErrorCode.ReservationArrivalCannotBeRecorded);
    }

    /// <summary>Masanın aktivliyini, tutumunu və bütün razılaşdırılmış vaxtı yoxlayır; çıxış vaxtını səssiz qısaltmır.</summary>
    private async Task<ECafe.Domain.Entities.Table> EnsureSeatingTableAvailableAsync(ReservationEntity reservation, int tableId, DateTime nowUtc)
    {
        if (ReservationArrivalPolicy.GetSeatingConflict(reservation, nowUtc) is { } conflict)
            throw new BusinessRuleException(conflict);
        var availability = await _tableRepository.GetReservationTableAvailabilityAsync(
            reservation.RestaurantId, tableId, new DateTimeOffset(DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc)), reservation.Id);
        if (availability == null || !availability.Table.IsActive || availability.Table.IsDeleted ||
            availability.Table.Capacity < reservation.PeopleCount)
            throw new BusinessRuleException(ErrorCode.TableAlreadyReserved);

        if (availability.MustVacateAt is { } availableUntil &&
            (availableUntil <= reservation.ReservedAt || !reservation.MustVacateAt.HasValue ||
             availableUntil < reservation.MustVacateAt.Value))
            throw new BusinessRuleException(ErrorCode.ReservationTableChangeTermsConflict);
        if (await _tableRepository.HasOpenTableSessionAsync(reservation.RestaurantId, tableId))
            throw new BusinessRuleException(ErrorCode.TableAlreadyReserved);
        return availability.Table;
    }

    /// <summary>Ayrılma və masa dəyişmə əməliyyatını yalnız faktiki gəlmiş, hələ əyləşməmiş qonağa məhdudlaşdırır.</summary>
    private static void EnsureWaitingGuest(ReservationEntity reservation)
    {
        if (!ReservationWaitingPolicy.IsWaiting(reservation))
            throw new BusinessRuleException(ErrorCode.ReservationWaitingRequired);
    }

    /// <summary>Kilid gözlənərkən masa dəyişibsə köhnə masanın kilidi altında yanlış əməliyyat aparılmasını dayandırır.</summary>
    private static void EnsureSnapshotTableUnchanged(ReservationEntity snapshot, ReservationEntity reservation)
    {
        if (snapshot.TableId != reservation.TableId)
            throw new BusinessRuleException(ErrorCode.ReservationTableChangedRetry);
    }

    /// <summary>Bildirişi yalnız restoranın aktiv, seçilmiş roldakı əməkdaşlarına göndərir; maliyyə məlumatı əlavə etmir.</summary>
    private async Task<int> NotifyReceptionStaffAsync(ReservationEntity reservation, IReadOnlyCollection<int> roleIds,
        NotificationType type, string title, string message)
    {
        var staff = await _userRestaurantRepository.GetActiveByRestaurantAndRolesAsync(reservation.RestaurantId, roleIds);
        var recipients = staff.Select(assignment => assignment.UserId).Distinct().ToList();
        foreach (var userId in recipients)
        {
            await _notificationService.CreateAsync(new CreateNotificationRequest
            {
                UserId = userId,
                RestaurantId = reservation.RestaurantId,
                TypeId = (int)type,
                ChannelId = (int)NotificationChannel.InApp,
                Title = title,
                Message = message,
                RelatedEntityType = AuditEntityTypes.Reservation,
                RelatedEntityId = reservation.Id,
                PayloadJson = JsonSerializer.Serialize(new
                {
                    restaurantId = reservation.RestaurantId, reservationId = reservation.Id,
                    tableId = reservation.TableId, section = ReservationReceptionTexts.WaitingSection
                })
            });
        }
        return recipients.Count;
    }
}
