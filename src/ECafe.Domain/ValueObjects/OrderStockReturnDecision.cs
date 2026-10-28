using ECafe.Domain.Enums;

namespace ECafe.Domain.ValueObjects;

public sealed record OrderStockReturnDecision(OrderStockReturnMode Mode, IReadOnlyList<OrderStockReturnQuantity> Returns);
