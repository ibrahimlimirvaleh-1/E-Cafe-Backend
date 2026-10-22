using ECafe.Application.DTOs.TableSession;
using ECafe.Domain.Entities;

namespace ECafe.Application.Mappings;

public static class WaiterReadyOrderMapper
{
    /// <summary>Hazır növbədə icazəsi yoxlanmış sifarişi mövcud sifariş mapper-i və cari masa konteksti ilə qaytarır.</summary>
    public static WaiterReadyOrderResponse Map(Order order) => new(OrderResponseMapper.Map(order),
        order.TableSession!.Table.Name, order.TableSession.Table.TableNo, order.TableSession.Restaurant.TimeZone,
        OrderMappingValues.AsUtc(order.ReadyAt!.Value));
}
