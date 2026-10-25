using ECafe.Application.DTOs.Order;
using ECafe.Domain.Enums;
using ECafe.Domain.Policies;
using static ECafe.Application.Mappings.OrderMappingValues;

namespace ECafe.Application.Mappings;

public static class KitchenOrderResponseMapper
{
    /// <summary>Məhsul nüsxələrini və faktiki tarixləri qiymətsiz mətbəx görünüşünə çevirir; növbəti addımları domain qaydası ilə hesablayır.</summary>
    public static KitchenOrderResponse Map(Domain.Entities.Order order, bool? canDecideCancellationRequest = null)
        => new(order.Id, order.RestaurantId, order.TableId, TableName(order.Table), order.TableSessionId, order.WaiterUserId,
            order.StatusId, AsUtc(order.OpenedAt), AsUtc(order.SentToKitchenAt!.Value), order.Note,
            order.OrderItems.Where(line => !line.IsDeleted).OrderBy(line => line.Id).Select(line => new KitchenOrderLineResponse(
                line.Id, line.ItemId, line.NameSnapshot, line.Quantity, line.Note)).ToList(), order.Restaurant.TimeZone,
            AsNullableUtc(order.AcceptedAt), AsNullableUtc(order.PreparingAt), AsNullableUtc(order.ReadyAt),
            order.AcceptedByUserId, order.PreparingByUserId, order.ReadyByUserId,
            KitchenOrderProgressPolicy.CanApply(order, OrderStatus.Accepted),
            KitchenOrderProgressPolicy.CanApply(order, OrderStatus.Preparing),
            KitchenOrderProgressPolicy.CanApply(order, OrderStatus.Ready), canDecideCancellationRequest ?? OrderCancellationRequestPolicy.CanDecide(order),
            OrderCancellationRequestMapper.Map(order.CancellationRequest));
}
