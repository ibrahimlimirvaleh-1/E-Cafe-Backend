using System.Data;
using System.Text.Json;
using AutoMapper;
using ECafe.Application.Common.Audit;
using ECafe.Application.Common.Exceptions;
using ECafe.Application.DTOs.Notification;
using ECafe.Application.DTOs.Restaurant;
using ECafe.Application.Repositories.Restaurant;
using ECafe.Application.Repositories.UserRestaurant;
using ECafe.Application.Repository;
using ECafe.Application.Services.AuditLog.Abstract;
using ECafe.Application.Services.Notification.Abstract;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace ECafe.Application.Services.Restaurant.Schedule;

public sealed class RestaurantScheduleManager(
    IHttpContextAccessor http, IMapper mapper, IConfiguration configuration,
    IRestaurantScheduleRepository repository, IApplicationDbTransactionFactory transactions,
    IUserRestaurantRepository assignments, INotificationService notifications,
    IAuditLogService audit, TimeProvider clock)
    : BaseManager(http, mapper, configuration), IRestaurantScheduleService
{
    public async Task<ScheduleChangeResponse?> GetAsync(int restaurantId, CancellationToken ct)
    {
        await AuthorizeManagerAsync(restaurantId);
        var change = await repository.GetLatestAsync(restaurantId, ct);
        if (change == null) return null;
        var reservations = await repository.GetActiveReservationsAsync(restaurantId, Now, ct);
        var sessions = await repository.GetOpenSessionsAsync(restaurantId, ct);
        var active = change.Consents.Where(c => c.ReservationId.HasValue
            ? reservations.Any(r => r.Id == c.ReservationId)
            : sessions.Any(s => s.Id == c.TableSessionId)).ToList();
        return Map(change, active);
    }

    public async Task<ScheduleChangeResponse> ProposeAsync(int restaurantId, ProposeScheduleRequest request, CancellationToken ct)
    {
        await AuthorizeManagerAsync(restaurantId);
        Validate(request);
        await using var transaction = await transactions.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        await repository.AcquireLocksAsync(restaurantId, ct);
        var restaurant = await repository.GetRestaurantAsync(restaurantId, ct)
            ?? throw new NotFoundException(ErrorCode.RestaurantNotFound);
        if (await repository.GetPendingAsync(restaurantId, ct) != null)
            throw new BusinessRuleException(ErrorCode.ScheduleChangeAlreadyPending);
        var hours = ScheduleTerms.Normalize(request.WorkingHours);
        if (ScheduleTerms.Equivalent(restaurant.WorkingHours, hours))
            throw new BusinessRuleException(ErrorCode.ScheduleUnchanged);
        var change = new RestaurantScheduleChange
        {
            RestaurantId = restaurantId, Restaurant = restaurant,
            ProposedHoursJson = ScheduleTerms.Serialize(hours), Reason = request.Reason.Trim(),
            Token = Guid.NewGuid(), RequestedByUserId = GetCurrentUserId()
        };
        await repository.AddAsync(change, ct);
        await repository.SaveAsync(ct);
        var active = await RefreshAsync(change, ct);
        await RecordAsync(change, AuditActions.RestaurantScheduleProposed);
        await NotifyManagersAsync(change, "İş saatı dəyişikliyi hazırlanıb");
        await transaction.CommitAsync(ct);
        return Map(change, active);
    }

    public async Task<ScheduleChangeResponse> ApplyAsync(int restaurantId, ScheduleActionRequest request, CancellationToken ct)
    {
        await AuthorizeManagerAsync(restaurantId);
        await using var transaction = await transactions.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        await repository.AcquireLocksAsync(restaurantId, ct);
        var change = await RequirePendingAsync(restaurantId, request.Token, ct);
        var active = await RefreshAsync(change, ct);
        // Preserve any new blockers in the response; do not apply partially.
        if (active.Any(c => c.State != ScheduleConsentState.Accepted || !CanAccept(c)))
        {
            await transaction.CommitAsync(ct);
            return Map(change, active);
        }
        var restaurant = await repository.GetRestaurantAsync(restaurantId, ct)
            ?? throw new NotFoundException(ErrorCode.RestaurantNotFound);
        ScheduleTerms.Apply(restaurant, ScheduleTerms.Deserialize(change.ProposedHoursJson));
        foreach (var consent in active.Where(c => c.ReservationId.HasValue))
        {
            var reservation = consent.Reservation!;
            reservation.MustVacateAt = consent.ProposedVacateAt;
            if (reservation.ArrivalAdjustment?.AcceptedAt != null)
                reservation.ArrivalAdjustment.MustVacateAt = consent.ProposedVacateAt;
            reservation.StatusHistory.Add(new ReservationStatusHistory
            {
                FromStatusId = reservation.StatusId, ToStatusId = reservation.StatusId,
                ChangedAt = Now, ChangedByUserId = consent.RespondedByUserId,
                Reason = "Müştərinin razılığı ilə restoranın yeni bağlanış vaxtı tətbiq edildi."
            });
        }
        change.State = ScheduleChangeState.Applied;
        change.AppliedAt = Now;
        await repository.SaveAsync(ct);
        await RecordAsync(change, AuditActions.RestaurantScheduleApplied);
        foreach (var consent in active.Where(c => c.ReservationId.HasValue))
            await NotifyCustomerAsync(change, consent, "İş saatı dəyişikliyi tətbiq edildi",
                "Qəbul etdiyiniz masa təhvil şərti qüvvəyə mindi.");
        await NotifyManagersAsync(change, "İş saatları yeniləndi");
        await transaction.CommitAsync(ct);
        return Map(change, active);
    }

    public async Task<ScheduleChangeResponse> WithdrawAsync(int restaurantId, ScheduleActionRequest request, CancellationToken ct)
    {
        await AuthorizeManagerAsync(restaurantId);
        await using var transaction = await transactions.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        await repository.AcquireLocksAsync(restaurantId, ct);
        var change = await RequirePendingAsync(restaurantId, request.Token, ct);
        change.State = ScheduleChangeState.Withdrawn;
        await repository.SaveAsync(ct);
        await RecordAsync(change, AuditActions.RestaurantScheduleWithdrawn);
        foreach (var consent in change.Consents.Where(c => c.ReservationId.HasValue))
            await NotifyCustomerAsync(change, consent, "İş saatı dəyişikliyi geri götürüldü",
                "Əvvəlki rezervasiya şərtləriniz qüvvədə qalır.");
        await NotifyManagersAsync(change, "İş saatı dəyişikliyi geri götürüldü");
        await transaction.CommitAsync(ct);
        return Map(change, []);
    }

    public async Task<CustomerScheduleOfferResponse?> GetCustomerOfferAsync(int reservationId, CancellationToken ct)
    {
        var consent = await repository.GetCustomerConsentAsync(reservationId, GetCurrentUserId(), ct);
        if (consent == null) return null;
        var response = MapCustomer(consent);
        if (!response.CanRespond) return response;
        var active = await repository.GetActiveReservationsAsync(consent.ScheduleChange.RestaurantId, Now, ct);
        return active.Any(r => r.Id == reservationId) ? response : response with { CanRespond = false, CanAccept = false };
    }

    public async Task<CustomerScheduleOfferResponse> RespondAsync(int reservationId, ScheduleDecisionRequest request, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var snapshot = await repository.GetCustomerConsentAsync(reservationId, userId, ct)
            ?? throw new NotFoundException(ErrorCode.ScheduleOfferNotFound);
        await using var transaction = await transactions.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        await repository.AcquireLocksAsync(snapshot.ScheduleChange.RestaurantId, ct);
        var change = await RequirePendingAsync(snapshot.ScheduleChange.RestaurantId, request.Token, ct);
        var active = await RefreshAsync(change, ct);
        var consent = active.SingleOrDefault(c => c.ReservationId == reservationId && c.Reservation!.CustomerUserId == userId)
            ?? throw new BusinessRuleException(ErrorCode.ScheduleOfferNotAvailable);
        if (!Decide(consent, request, userId))
        {
            await transaction.CommitAsync(ct);
            return MapCustomer(consent);
        }
        await repository.SaveAsync(ct);
        await RecordAsync(change, AuditActions.RestaurantScheduleCustomerResponded);
        await NotifyManagersAsync(change, $"Rezervasiya #{reservationId}: iş saatı təklifinə cavab verildi");
        await NotifyCustomerAsync(change, consent, "Cavabınız qeydə alındı",
            request.Accept ? "Razılığınız qeydə alındı. Dəyişiklik tətbiq edilənədək əvvəlki şərtlər qüvvədədir."
                : "Təklifi qəbul etmədiniz. Rezervasiyanız avtomatik ləğv edilməyib; restoran məsələni həll edəcək.");
        await transaction.CommitAsync(ct);
        return MapCustomer(consent);
    }

    public async Task<ScheduleChangeResponse> AcknowledgeSessionAsync(int restaurantId, int consentId,
        ScheduleDecisionRequest request, CancellationToken ct)
    {
        await AuthorizeManagerAsync(restaurantId);
        if (string.IsNullOrWhiteSpace(request.Note) || request.Note.Length > 1000)
            throw new BusinessRuleException(ErrorCode.ScheduleSessionNoteRequired);
        await using var transaction = await transactions.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        await repository.AcquireLocksAsync(restaurantId, ct);
        var change = await RequirePendingAsync(restaurantId, request.Token, ct);
        var active = await RefreshAsync(change, ct);
        var consent = active.SingleOrDefault(c => c.Id == consentId && c.TableSessionId.HasValue)
            ?? throw new BusinessRuleException(ErrorCode.ScheduleOfferNotAvailable);
        if (!Decide(consent, request, GetCurrentUserId()))
        {
            await transaction.CommitAsync(ct);
            return Map(change, active);
        }
        await repository.SaveAsync(ct);
        await RecordAsync(change, AuditActions.RestaurantScheduleWalkInAcknowledged);
        await NotifyManagersAsync(change, "Masa sessiyası üzrə razılaşma qeyd edildi");
        await transaction.CommitAsync(ct);
        return Map(change, active);
    }

    public async Task EnsureDirectChangeAllowedAsync(Domain.Entities.Restaurant restaurant, List<RestaurantWorkingHour> hours,
        string timeZone, CancellationToken ct)
    {
        if (ScheduleTerms.Equivalent(restaurant.WorkingHours, hours) && restaurant.TimeZone == timeZone) return;
        await repository.AcquireLocksAsync(restaurant.Id, ct);
        if (await repository.GetPendingAsync(restaurant.Id, ct) != null)
            throw new BusinessRuleException(ErrorCode.ScheduleChangeAlreadyPending);
        var reservations = await repository.GetActiveReservationsAsync(restaurant.Id, Now, ct);
        var sessions = await repository.GetOpenSessionsAsync(restaurant.Id, ct);
        var count = restaurant.TimeZone != timeZone ? reservations.Count + sessions.Count :
            reservations.Count(r => ScheduleTerms.IsAffected(restaurant.WorkingHours, hours, timeZone,
                r.ReservedAt, r.MustVacateAt, out _) || LateArrivalAffected(r, hours, timeZone)) +
            sessions.Count(s => ScheduleTerms.IsAffected(restaurant.WorkingHours, hours, timeZone, s.OpenedAt, null, out _));
        if (count > 0)
            throw new BusinessRuleException(ErrorCode.ScheduleChangeRequiresConsent, new { count });
    }

    // Saat dəyişikliyinin təsir etdiyi aktiv rezervasiya və sessiyaları yenidən hesablayır.
    private async Task<List<RestaurantScheduleConsent>> RefreshAsync(RestaurantScheduleChange change, CancellationToken ct)
    {
        var restaurant = await repository.GetRestaurantAsync(change.RestaurantId, ct)
            ?? throw new NotFoundException(ErrorCode.RestaurantNotFound);
        var proposed = ScheduleTerms.Deserialize(change.ProposedHoursJson);
        var active = new List<RestaurantScheduleConsent>();
        var reservations = await repository.GetActiveReservationsAsync(change.RestaurantId, Now, ct);
        foreach (var reservation in reservations)
        {
            var affected = ScheduleTerms.IsAffected(restaurant.WorkingHours, proposed, restaurant.TimeZone,
                reservation.ReservedAt, reservation.MustVacateAt, out var vacateAt);
            if (!affected && !LateArrivalAffected(reservation, proposed, restaurant.TimeZone)) continue;
            var consent = change.Consents.SingleOrDefault(c => c.ReservationId == reservation.Id);
            if (consent == null)
            {
                consent = new RestaurantScheduleConsent { ReservationId = reservation.Id, Reservation = reservation, ProposedVacateAt = vacateAt };
                change.Consents.Add(consent);
                await repository.SaveAsync(ct);
                await NotifyCustomerAsync(change, consent, "Restoran iş saatını dəyişmək istəyir",
                    "Rezervasiyanıza təsir edən təklif var. Qəbul və ya rədd etmək üçün rezervasiya detalını açın.");
            }
            RefreshTerms(consent, vacateAt);
            active.Add(consent);
        }
        foreach (var session in await repository.GetOpenSessionsAsync(change.RestaurantId, ct))
        {
            if (session.ReservationId.HasValue && active.Any(c => c.ReservationId == session.ReservationId)) continue;
            if (!ScheduleTerms.IsAffected(restaurant.WorkingHours, proposed, restaurant.TimeZone, session.OpenedAt, null, out var vacateAt)) continue;
            var consent = change.Consents.SingleOrDefault(c => c.TableSessionId == session.Id);
            if (consent == null)
            {
                consent = new RestaurantScheduleConsent { TableSessionId = session.Id, TableSession = session, ProposedVacateAt = vacateAt };
                change.Consents.Add(consent);
            }
            RefreshTerms(consent, vacateAt);
            active.Add(consent);
        }
        await repository.SaveAsync(ct);
        return active;
    }

    // Yeni bağlanma vaxtına görə müştəri razılığının təhvil şərtlərini yeniləyir.
    private static void RefreshTerms(RestaurantScheduleConsent consent, DateTime? vacateAt)
    {
        if (consent.ProposedVacateAt == vacateAt) return;
        consent.ProposedVacateAt = vacateAt;
        consent.State = ScheduleConsentState.Pending;
        consent.RespondedAt = null;
        consent.RespondedByUserId = null;
        consent.ResponseNote = null;
    }

    // Müştərinin təklifi indiki rezervasiya şərtləri ilə qəbul edə biləcəyini yoxlayır.
    private bool CanAccept(RestaurantScheduleConsent consent)
    {
        var end = consent.ProposedVacateAt;
        if (!end.HasValue || end <= Now) return false;
        var reservation = consent.Reservation;
        if (reservation == null) return true;
        var arrival = reservation.ArrivalAdjustment?.AcceptedAt != null
            ? reservation.ArrivalAdjustment.RequestedArrivalAt : reservation.ReservedAt;
        return arrival < end && (reservation.StatusId == StatusIds.Reservation(ReservationStatus.Seated) ||
            reservation.NoShowDeadlineAt < end);
    }

    // Müştərinin qərarını yalnız cavab verilə bilən təklifə tətbiq edir.
    private bool Decide(RestaurantScheduleConsent consent, ScheduleDecisionRequest request, int userId)
    {
        var state = request.Accept ? ScheduleConsentState.Accepted : ScheduleConsentState.Rejected;
        if (consent.State == state) return false;
        if (consent.State != ScheduleConsentState.Pending)
            throw new BusinessRuleException(ErrorCode.ScheduleOfferAlreadyAnswered);
        if (request.Note?.Length > 1000) throw new BusinessRuleException(ErrorCode.ScheduleInvalidRequest);
        if (request.Accept && !CanAccept(consent)) throw new BusinessRuleException(ErrorCode.ScheduleOfferCannotAccept);
        consent.State = state;
        consent.RespondedAt = Now;
        consent.RespondedByUserId = userId;
        consent.ResponseNote = request.Note?.Trim();
        return true;
    }

    // Gecikmiş gəliş təklifinin yeni iş saatlarından təsirlənməsini müəyyən edir.
    private static bool LateArrivalAffected(Domain.Entities.Reservation reservation,
        IEnumerable<RestaurantWorkingHour> proposed, string zone)
        => reservation.ArrivalAdjustment?.AcceptedAt != null && ScheduleTerms.IsAffected(proposed, proposed, zone,
            reservation.ArrivalAdjustment.RequestedArrivalAt, reservation.MustVacateAt, out _);

    // Yalnız cari və hələ gözlənilən saat dəyişikliyi təklifini qaytarır.
    private async Task<RestaurantScheduleChange> RequirePendingAsync(int restaurantId, Guid token, CancellationToken ct)
    {
        var change = await repository.GetPendingAsync(restaurantId, ct);
        if (change == null || token == Guid.Empty || change.Token != token)
            throw new BusinessRuleException(ErrorCode.ScheduleOfferNotAvailable);
        return change;
    }

    // Saat dəyişikliyini restorana səlahiyyətli şəxsə məhdudlaşdırır.
    private async Task AuthorizeManagerAsync(int restaurantId)
    {
        EnsureCurrentUserCanAccessRestaurant(restaurantId);
        if (IsCurrentUserSuperAdmin()) return;
        var role = await assignments.GetActiveRoleIdAsync(GetCurrentUserId(), restaurantId);
        if (role is not ((int)RoleCode.Manager) and not ((int)RoleCode.Owner))
            throw new ForbiddenException(ErrorCode.AccessDenied);
    }

    // Təklif edilən iş saatlarının və səbəbin tamlığını yoxlayır.
    private static void Validate(ProposeScheduleRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 1000 ||
            request.WorkingHours == null || request.WorkingHours.Count != 7 ||
            request.WorkingHours.Any(h => h == null) ||
            request.WorkingHours.Select(h => h.DayOfWeek).Distinct().Count() != 7 ||
            request.WorkingHours.Any(h => !Enum.IsDefined(h.DayOfWeek) || h.CloseDayOffset is < 0 or > 1 ||
                !h.IsClosed && h.CloseDayOffset.GetValueOrDefault(h.OpensAt > h.ClosesAt ? 1 : 0) == 0 &&
                h.ClosesAt != TimeOnly.MinValue && h.ClosesAt <= h.OpensAt))
            throw new BusinessRuleException(ErrorCode.ScheduleInvalidRequest);
    }

    // Saat dəyişikliyi və razılıqları idarəetmə cavabına çevirir.
    private ScheduleChangeResponse Map(RestaurantScheduleChange change, List<RestaurantScheduleConsent> active)
        => new(change.Id, change.Token, change.State.ToString(), change.Reason, change.Restaurant.TimeZone,
            JsonSerializer.Deserialize<List<RestaurantWorkingHourDto>>(change.ProposedHoursJson)!,
            active.Select(c => new ScheduleParticipantResponse(c.Id, c.ReservationId, c.TableSessionId,
                $"Masa-{c.Reservation?.Table.TableNo ?? c.TableSession!.Table.TableNo}",
                c.Reservation?.CustomerUser.Name, Offset(c.Reservation?.ReservedAt ?? c.TableSession!.OpenedAt),
                c.ProposedVacateAt.HasValue ? Offset(c.ProposedVacateAt.Value) : null,
                c.State.ToString(), CanAccept(c), c.ResponseNote)).ToList(),
            change.State == ScheduleChangeState.Pending && active.All(c => c.State == ScheduleConsentState.Accepted && CanAccept(c)));

    // Müştəriyə yalnız öz gəlişinə aid dəyişiklik şərtlərini göstərir.
    private CustomerScheduleOfferResponse MapCustomer(RestaurantScheduleConsent consent)
        => new(consent.ScheduleChangeId, consent.Id, consent.ScheduleChange.Token, consent.ScheduleChange.State == ScheduleChangeState.Pending
                ? consent.State.ToString() : consent.ScheduleChange.State.ToString(),
            consent.ScheduleChange.Reason, consent.ScheduleChange.Restaurant.TimeZone,
            consent.ProposedVacateAt.HasValue ? Offset(consent.ProposedVacateAt.Value) : null, CanAccept(consent),
            consent.State == ScheduleConsentState.Pending && consent.ScheduleChange.State == ScheduleChangeState.Pending &&
            IsActiveStatus(consent.Reservation!.StatusId));

    // Saat dəyişikliklərində hələ qüvvədə sayılan rezervasiya statuslarını ayırır.
    private static bool IsActiveStatus(int statusId) => new[] { ReservationStatus.Confirmed, ReservationStatus.Seated,
        ReservationStatus.AwaitingPaymentInstruction, ReservationStatus.PendingPayment, ReservationStatus.PaymentSubmitted }
        .Any(status => StatusIds.Reservation(status) == statusId);

    // Təklif və qərar barədə rezervasiya sahibini məlumatlandırır.
    private Task NotifyCustomerAsync(RestaurantScheduleChange change, RestaurantScheduleConsent consent, string title, string message)
        => notifications.CreateAsync(new CreateNotificationRequest
        {
            UserId = consent.Reservation!.CustomerUserId, RestaurantId = change.RestaurantId,
            Title = title, Message = message, TypeId = (int)NotificationType.RestaurantScheduleChanged,
            ChannelId = (int)NotificationChannel.InApp, RelatedEntityType = AuditEntityTypes.Reservation,
            RelatedEntityId = consent.ReservationId,
            PayloadJson = JsonSerializer.Serialize(new { reservationId = consent.ReservationId, restaurantId = change.RestaurantId, section = "schedule-offer" })
        });

    // Saat dəyişikliyinin vəziyyətini restoran məsullarına çatdırır.
    private async Task NotifyManagersAsync(RestaurantScheduleChange change, string title)
    {
        var users = await assignments.GetActiveByRestaurantAndRolesAsync(change.RestaurantId,
            [(int)RoleCode.Manager, (int)RoleCode.Owner]);
        foreach (var userId in users.Select(a => a.UserId).Append(change.RequestedByUserId).Distinct())
            await notifications.CreateAsync(new CreateNotificationRequest
            {
                UserId = userId, RestaurantId = change.RestaurantId, Title = title, Message = change.Reason,
                TypeId = (int)NotificationType.RestaurantScheduleChanged, ChannelId = (int)NotificationChannel.InApp,
                RelatedEntityType = AuditEntityTypes.Restaurant, RelatedEntityId = change.RestaurantId,
                PayloadJson = JsonSerializer.Serialize(new { restaurantId = change.RestaurantId, section = "schedule-change" })
            });
    }

    // Saat dəyişikliyi əməliyyatını audit tarixçəsinə yazır.
    private Task RecordAsync(RestaurantScheduleChange change, string action)
        => audit.RecordRestaurantActionAsync(change.RestaurantId, action,
            new { change.Id, change.State, change.Reason }, AuditEntityTypes.Restaurant, change.RestaurantId);
    private DateTime Now => clock.GetUtcNow().UtcDateTime;
    // Bazadakı UTC vaxtını API üçün saat qurşağı göstərilən formaya çevirir.
    private static DateTimeOffset Offset(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
}
