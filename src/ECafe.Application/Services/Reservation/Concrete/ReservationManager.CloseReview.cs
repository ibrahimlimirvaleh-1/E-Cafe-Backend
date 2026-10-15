using ECafe.Application.Common.Exceptions;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;

namespace ECafe.Application.Services.Reservation.Concrete;

public sealed partial class ReservationManager
{
    /// <summary>Rezervasiyanın açıq sessiyasını tapır və köhnə qeydin işgününü hesablamaq üçün yüklənmiş restoranı əlaqələndirir.</summary>
    private static TableSession? FindOpenSession(Domain.Entities.Reservation reservation)
    {
        var session = reservation.TableSessions.FirstOrDefault(item => !item.IsDeleted && item.ClosedAt == null &&
            item.StatusId == StatusIds.TableSession(TableSessionStatus.Open));
        if (session != null) session.Restaurant = reservation.Restaurant;
        return session;
    }

    /// <summary>Frontend üçün bağlama idarəsini yalnız menecer/sahibkar və sessiyanın öz ofisiantı kontekstində göstərir; yazma ayrıca DB-dən yoxlanılır.</summary>
    private bool CanCompleteSession(TableSession? session)
    {
        if (session == null) return false;
        var role = (RoleCode)GetCurrentRoleId(session.RestaurantId);
        return role is RoleCode.Manager or RoleCode.Owner || role == RoleCode.Waiter && session.WaiterUserId == GetCurrentUserId();
    }

    /// <summary>Bağlama anında aktiv DB rolunu yoxlayır; ofisiant axınına menecer səlahiyyətləri vermir və köhnə tokenə etibar etmir.</summary>
    private async Task EnsureCompletionRoleAsync(int restaurantId, bool waiterAction)
    {
        EnsureCurrentUserCanAccessRestaurant(restaurantId);
        var role = await _userRestaurantRepository.GetActiveRoleIdAsync(GetCurrentUserId(), restaurantId);
        if (waiterAction ? role != (int)RoleCode.Waiter : role != (int)RoleCode.Manager && role != (int)RoleCode.Owner)
            throw new ForbiddenException(ErrorCode.RestaurantServiceActionForbidden);
    }
}
