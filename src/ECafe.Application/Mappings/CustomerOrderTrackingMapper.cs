using ECafe.Application.DTOs.Order;
using static ECafe.Application.Mappings.OrderMappingValues;

namespace ECafe.Application.Mappings;

public static class CustomerOrderTrackingMapper
{
    /// <summary>Yalnız müştəriyə lazım olan sessiya sahələrini seçir; daxili qeydlər, işçi ID-ləri və maliyyə rekvizitləri yüklənmir.</summary>
    public static IQueryable<CustomerTableSessionResponse> ProjectSessions(IQueryable<Domain.Entities.TableSession> query)
        => query.Select(session => new CustomerTableSessionResponse(session.Id, session.RestaurantId,
            session.Restaurant.Name, session.TableId, TableName(session.Table), session.ReservationId,
            session.StatusId, AsUtc(session.OpenedAt), AsNullableUtc(session.ClosedAt), session.Restaurant.TimeZone));

    /// <summary>Menyu dəyişsə də saxlanılmış məhsul/qiymət nüsxələrini və faktiki mərhələ vaxtlarını göstərir; daxili qeydləri və əməkdaş məlumatlarını çıxarmır.</summary>
    public static IQueryable<CustomerSessionOrderResponse> ProjectOrders(IQueryable<Domain.Entities.Order> query)
        => query.Select(order => new CustomerSessionOrderResponse(order.Id, order.RestaurantId, order.TableId,
            order.TableSessionId!.Value, order.ReservationId, order.StatusId, AsUtc(order.OpenedAt),
            AsNullableUtc(order.SentToKitchenAt), AsNullableUtc(order.AcceptedAt), AsNullableUtc(order.PreparingAt),
            AsNullableUtc(order.ReadyAt), AsNullableUtc(order.ServedAt), AsNullableUtc(order.ClosedAt),
            order.OrderItems.Where(line => !line.IsDeleted).Sum(line => line.LineTotal),
            order.OrderItems.Where(line => !line.IsDeleted).OrderBy(line => line.Id)
                .Select(line => new CustomerOrderLineResponse(line.Id, line.ItemId, line.NameSnapshot,
                    line.Quantity, line.UnitPriceSnapshot, line.LineTotal)).ToList()));
}
