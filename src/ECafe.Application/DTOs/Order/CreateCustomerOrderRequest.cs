namespace ECafe.Application.DTOs.Order;

public sealed class CreateCustomerOrderRequest
{
    public string? Note { get; init; }
    public List<CreateOrderLineRequest> Items { get; init; } = [];
}

public sealed record CreateCustomerOrderResult(CustomerSessionOrderResponse Order, bool IsReplay);
