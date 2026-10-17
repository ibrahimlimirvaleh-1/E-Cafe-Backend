using ECafe.Application.DTOs.Order;
using ECafe.Domain.Policies;
using static ECafe.Application.Mappings.OrderMappingValues;

namespace ECafe.Application.Mappings;

public static class OrderResponseMapper
{
    /// <summary>İcazəsi yoxlanmış ofisiant görünüşünə qiymət/ad nüsxələrini, təqdim vaxtını və eyni biznes qaydasından növbəti addım işarəsini qaytarır.</summary>
    public static SessionOrderResponse Map(Domain.Entities.Order order)
    {
        var items = order.OrderItems.Where(item => !item.IsDeleted).OrderBy(item => item.Id)
            .Select(item => new OrderLineResponse(item.Id, item.ItemId, item.NameSnapshot,
                item.Quantity, item.UnitPriceSnapshot, item.LineTotal, item.Note)).ToList();
        return new SessionOrderResponse(order.Id, order.RestaurantId, order.TableId, order.TableSessionId,
            order.ReservationId, order.WaiterUserId, order.SourceId, order.StatusId,
            AsUtc(order.OpenedAt), order.SentToKitchenAt.HasValue ? AsUtc(order.SentToKitchenAt.Value) : null,
            order.Note, items.Sum(item => item.LineTotal), items,
            order.ServedAt.HasValue ? AsUtc(order.ServedAt.Value) : null, OrderServingPolicy.CanMarkServed(order));
    }

}
