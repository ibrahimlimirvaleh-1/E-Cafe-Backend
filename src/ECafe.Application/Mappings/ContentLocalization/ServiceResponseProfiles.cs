using ECafe.Application.DTOs.Order;
using ECafe.Application.DTOs.Reservation;
using ECafe.Application.DTOs.TableSession;
using ECafe.Application.Repositories.ContentLocalization;
using ECafe.Domain.Enums;

namespace ECafe.Application.Mappings.ContentLocalization;

internal static class ServiceResponseProfiles
{
    public static IReadOnlyList<IContentResponseProfile> All { get; } =
    [
        new ContentResponseProfile<ReservationResponse>(
            new ContentResponseField<ReservationResponse>(ContentEntityType.Table, (r, _) => r.TableId, r => r.TableName, v => v.Name)),
        new ContentResponseProfile<ReservationServiceItemResponse>(
            new ContentResponseField<ReservationServiceItemResponse>(ContentEntityType.Table, (r, _) => r.TableId, r => r.TableName, v => v.Name)),
        new ContentResponseProfile<WaiterTableSessionResponse>(
            new ContentResponseField<WaiterTableSessionResponse>(ContentEntityType.Table, (r, _) => r.TableId, r => r.TableName, v => v.Name)),
        new ContentResponseProfile<CustomerTableSessionResponse>(
            new ContentResponseField<CustomerTableSessionResponse>(ContentEntityType.Table, (r, _) => r.TableId, r => r.TableName, v => v.Name)),
        new ContentResponseProfile<KitchenOrderResponse>(
            new ContentResponseField<KitchenOrderResponse>(ContentEntityType.Table, (r, _) => r.TableId, r => r.TableName, v => v.Name))
    ];
}
