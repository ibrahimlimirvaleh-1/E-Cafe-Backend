using ECafe.Domain.Entities;
using ECafe.Domain.Exceptions;

namespace ECafe.Domain.Policies;

public static class InventoryCostPolicy
{
    public const decimal MaximumPurchaseUnitPrice = 1_000_000m;
    public const int PurchasePriceScale = 6;
    public const int AmountPrecision = 26;
    public const int AmountScale = 8;
    private const decimal MaximumValue = 999999999999999999.99999999m;

    /// <summary>Alış məbləğini sorğudakı orijinal miqdar və vahid qiymətindən, stok vahidinə çevirmədən hesablayır.</summary>
    public static decimal? PurchaseAmount(decimal quantity, decimal? unitPrice)
    {
        if (unitPrice is not decimal price) return null;
        if (price < 0 || price > MaximumPurchaseUnitPrice || decimal.Round(price, PurchasePriceScale) != price ||
            quantity <= 0 || (price > 1m && quantity > MaximumValue / price))
            throw new BusinessRuleException(ErrorCode.InventoryPurchasePriceInvalid);
        var value = RoundAmount(quantity * price);
        if (value > MaximumValue) throw new BusinessRuleException(ErrorCode.InventoryPurchasePriceInvalid);
        return value;
    }

    /// <summary>Məlum dəyəri qalığa əlavə edir; qiyməti məlum olmayan müsbət stokla qarışıq qalıq da naməlum qalır.</summary>
    public static void Add(InventoryItem item, decimal quantity, decimal? incomingValue)
    {
        var priorValue = item.QuantityOnHand == 0 ? 0m : item.StockCostValue;
        var newValue = priorValue + incomingValue;
        if (newValue > MaximumValue) throw new BusinessRuleException(ErrorCode.InventoryPurchasePriceInvalid);
        InventoryStockPolicy.ApplyChange(item, quantity);
        item.StockCostValue = newValue;
    }

    /// <summary>Çıxılan miqdara çəkili orta maya ayırır və stok sıfırlananda son yuvarlaqlaşdırma qalığını da çıxır.</summary>
    public static decimal? Remove(InventoryItem item, decimal quantity)
    {
        var before = item.QuantityOnHand;
        InventoryStockPolicy.ApplyChange(item, -quantity);
        var cost = Allocate(item.StockCostValue, quantity, before);
        item.StockCostValue = item.QuantityOnHand == 0 ? null : item.StockCostValue - cost;
        return cost;
    }

    /// <summary>Sərf edilmiş və ya qaytarılan miqdara tarixi dəyərin mütənasib hissəsini ayırır.</summary>
    public static decimal? Allocate(decimal? value, decimal quantity, decimal fullQuantity)
        => value is not decimal amount || fullQuantity <= 0 ? null
            : quantity == fullQuantity ? amount : RoundAmount(amount * (quantity / fullQuantity));

    /// <summary>Stok uçotunda subqəpik məbləğlərini saxlayır və sütun dəqiqliyinə uyğunlaşdırır.</summary>
    public static decimal RoundAmount(decimal amount)
        => decimal.Round(amount, AmountScale, MidpointRounding.AwayFromZero);
}
