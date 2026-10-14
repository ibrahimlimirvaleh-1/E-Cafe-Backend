using ECafe.Application.DTOs.Order;

namespace ECafe.Application.Services.Orders;

public static class OrderResponseMapper
{
    /// <summary>Sifarişin saxlanılmış qiymət və ad nüsxələrini qaytarır; cari menyudan yenidən qiymət hesablamır.</summary>
    public static SessionOrderResponse Map(Domain.Entities.Order order)
    {
        var items = order.OrderItems.Where(item => !item.IsDeleted).OrderBy(item => item.Id)
            .Select(item => new OrderLineResponse(item.Id, item.ItemId, item.NameSnapshot,
                item.Quantity, item.UnitPriceSnapshot, item.LineTotal, item.Note)).ToList();
        return new SessionOrderResponse(order.Id, order.RestaurantId, order.TableId, order.TableSessionId,
            order.ReservationId, order.WaiterUserId, order.SourceId, order.StatusId,
            AsUtc(order.OpenedAt), order.SentToKitchenAt.HasValue ? AsUtc(order.SentToKitchenAt.Value) : null,
            order.Note, items.Sum(item => item.LineTotal), items);
    }

    /// <summary>Bazada UTC kimi saxlanan vaxtı API üçün sıfır offset ilə göstərir.</summary>
    private static DateTimeOffset AsUtc(DateTime value)
        => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
}
