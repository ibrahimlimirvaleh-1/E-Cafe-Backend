using ECafe.Application.DTOs.Reservation;
using ECafe.Domain.Constants;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Shared.Extensions;

namespace ECafe.Application.Mappings;

public static class ReservationHistoryResponseMapper
{
    /// <summary>Saxlanmış tarixçəni dəyişmədən cavaba çevirir; ilkin sistem qeydi üçün ayrıca tərcümə olunan görünüş ayırır.</summary>
    public static ReservationHistoryResponse Map(Reservation reservation)
        => new()
        {
            ReservationId = reservation.Id,
            ArrivedAt = ToUtcOffset(reservation.ArrivedAt),
            SeatedAt = ToUtcOffset(reservation.SeatedAt),
            Items = reservation.StatusHistory.OrderBy(history => history.ChangedAt).ThenBy(history => history.Id)
                .Select(history => new ReservationHistoryItemResponse
                {
                    Id = history.Id,
                    FromStatus = history.FromStatus?.Name,
                    ToStatus = history.ToStatus?.Name ?? ReservationStatus.PendingPayment.GetName(),
                    ChangedAt = ToUtcOffset(history.ChangedAt)!.Value,
                    ActorType = history.ChangedByUserId is null ? "System"
                        : history.ChangedByUserId == reservation.CustomerUserId ? "Customer" : "Restaurant",
                    Reason = history.Reason,
                    ReasonLabel = ResolveGeneratedReason(history, reservation.CustomerUserId)
                }).ToList()
        };

    /// <summary>Yalnız müştərinin ilkin yaradılma keçidində serverin sabit yazdığı qeydi seçir; sonrakı və sərbəst qeydləri tərcüməyə vermir.</summary>
    private static string? ResolveGeneratedReason(ReservationStatusHistory history, int customerUserId)
    {
        if (history.FromStatusId.HasValue || history.ChangedByUserId != customerUserId) return null;
        if (history.ToStatusId == StatusIds.Reservation(ReservationStatus.Confirmed) &&
            history.Reason == ReservationHistoryTexts.NoDepositConfirmed)
            return ReservationHistoryTexts.NoDepositConfirmed;
        if ((history.ToStatusId == StatusIds.Reservation(ReservationStatus.AwaitingPaymentInstruction) ||
             history.ToStatusId == StatusIds.Reservation(ReservationStatus.PendingPayment)) &&
            history.Reason == ReservationHistoryTexts.Created)
            return ReservationHistoryTexts.Created;
        return null;
    }

    /// <summary>Tarixçənin UTC vaxtını API offset formatında saxlayır; hələ baş verməyən hadisə üçün null qaytarır.</summary>
    private static DateTimeOffset? ToUtcOffset(DateTime? value)
        => value.HasValue ? new DateTimeOffset(DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)) : null;
}
