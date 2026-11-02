using ECafe.Application.DTOs.Order;

namespace ECafe.Application.Mappings;

public static class CustomerOrderRequestMapper
{
    /// <summary>Müştəri sorğusunu eyni menyu, miqdar və qeyd yoxlamalarından keçirmək üçün ortaq yaratma girişinə çevirir.</summary>
    public static CreateWaiterOrderRequest ToCreationRequest(CreateCustomerOrderRequest request)
        => new() { Note = request.Note, Items = request.Items };
}
