using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Infrastructure.Context;
using ECafe.Application.Common.Audit;
using ECafe.Application.Features.MobileApp;
using Microsoft.EntityFrameworkCore;

namespace ECafe.Infrastructure.Services;

internal static class MobilePushEligibilityPolicy
{
    private static readonly int[] StaffRoleIds =
        [(int)RoleCode.Owner, (int)RoleCode.Manager, (int)RoleCode.Waiter, (int)RoleCode.Kitchen];

    /// <summary>
    /// Push qeydiyyatı və göndərişi üçün eyni icazəni tətbiq edir: aktiv işçi aidiyyəti,
    /// adminin açdığı restoran mobil modulu və qüvvədə olan müqavilə tələb olunur.
    /// Restoran göstərildikdə başqa restorandakı aidiyyət icazə hesab edilmir.
    /// </summary>
    public static IQueryable<UserRestaurant> EligibleStaff(ECafeDbContext context, int? restaurantId = null)
    {
        var activeContractStatusId = StatusIds.Contract(ContractStatus.Active);
        return context.UserRestaurants.Where(assignment =>
            (!restaurantId.HasValue || assignment.RestaurantId == restaurantId.Value) &&
            assignment.IsActive && assignment.User.IsActive &&
            StaffRoleIds.Contains(assignment.RoleId) &&
            assignment.Restaurant.IsActive && assignment.Restaurant.MobilePushEnabled &&
            assignment.Restaurant.ShowMobileDownloadLink &&
            assignment.Restaurant.Contracts.Any(contract => contract.StatusId == activeContractStatusId));
    }

    /// <summary>Aktiv müştəri hesablarını bazadakı cari rola görə seçir; restoranın işçi push icazəsi tələb olunmur.</summary>
    public static IQueryable<User> EligibleCustomers(ECafeDbContext context)
        => context.Users.Where(user => user.IsActive && user.RoleId == (int)RoleCode.Customer);

    /// <summary>
    /// Müştəriyə yalnız server bildirişində göstərilmiş öz rezervasiya, refund, sifariş və ya masa sessiyası üçün push icazəsi verir.
    /// Digər hesablar üçün bildirişin dəqiq restoranına aid aktiv işçi icazəsini yoxlayır.
    /// </summary>
    public static async Task<bool> CanReceiveAsync(ECafeDbContext context,
        MobilePushOutboxPayload payload, CancellationToken cancellationToken)
    {
        if (await EligibleCustomers(context).AnyAsync(user => user.Id == payload.UserId, cancellationToken))
            return await OwnsCustomerResourceAsync(context, payload, cancellationToken);

        return await EligibleStaff(context, payload.RestaurantId)
            .AnyAsync(assignment => assignment.UserId == payload.UserId, cancellationToken);
    }

    /// <summary>
    /// Serverin növbəyə yazdığı obyektin həmin müştəri və restorana aidliyini təkrar yoxlayır.
    /// Köhnə, obyekt əlaqəsi olmayan işçi hadisələri müştəri roluna keçid ilə telefon bildirişinə çevrilmir.
    /// </summary>
    private static Task<bool> OwnsCustomerResourceAsync(ECafeDbContext context,
        MobilePushOutboxPayload payload, CancellationToken cancellationToken)
    {
        if (payload.RelatedEntityId is not > 0 or > int.MaxValue)
            return Task.FromResult(false);

        var entityId = (int)payload.RelatedEntityId.Value;
        return payload.RelatedEntityType switch
        {
            AuditEntityTypes.Reservation => context.Reservations.AnyAsync(reservation =>
                reservation.Id == entityId && reservation.RestaurantId == payload.RestaurantId &&
                reservation.CustomerUserId == payload.UserId, cancellationToken),
            AuditEntityTypes.ReservationRefund => context.ReservationRefunds.AnyAsync(refund =>
                refund.Id == entityId && refund.Reservation.RestaurantId == payload.RestaurantId &&
                refund.Reservation.CustomerUserId == payload.UserId, cancellationToken),
            AuditEntityTypes.Order => context.Orders.AnyAsync(order =>
                order.Id == entityId && order.RestaurantId == payload.RestaurantId &&
                order.CustomerUserId == payload.UserId, cancellationToken),
            AuditEntityTypes.TableSession => context.TableSessions.AnyAsync(session =>
                session.Id == entityId && session.RestaurantId == payload.RestaurantId &&
                session.CustomerUserId == payload.UserId, cancellationToken),
            _ => Task.FromResult(false)
        };
    }
}
