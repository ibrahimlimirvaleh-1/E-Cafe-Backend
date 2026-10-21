using ECafe.Domain.Entities;
using ECafe.Domain.Exceptions;

namespace ECafe.Domain.Policies;

public static class InventoryStockPolicy
{
    public const decimal MaximumQuantity = 999999999999.999999m;

    /// <summary>Eyni ölçü ailəsində resept miqdarını anbar vahidinə çevirir; sıfır və səhv çevirmə əmsalını qəbul etmir.</summary>
    public static decimal ConvertQuantity(decimal quantity, Unit source, Unit target)
    {
        if (quantity <= 0 || source.IsDeleted || target.IsDeleted || source.ConversionRateToBase <= 0 ||
            target.ConversionRateToBase <= 0 || (source.BaseUnitId ?? source.Id) != (target.BaseUnitId ?? target.Id))
            throw new BusinessRuleException(ErrorCode.InventoryUnitConversionNotAllowed);
        try
        {
            var converted = source.Id == target.Id ? quantity : checked(quantity * source.ConversionRateToBase / target.ConversionRateToBase);
            if (converted > MaximumQuantity) throw new BusinessRuleException(ErrorCode.InventoryStockQuantityInvalid);
            return converted;
        }
        catch (OverflowException) { throw new BusinessRuleException(ErrorCode.InventoryStockQuantityInvalid); }
    }

    /// <summary>Anbar dəqiqliyindən kiçik istehlakı itirmir; altı onluq mərtəbəyə yuxarı yuvarlaqlaşdırır.</summary>
    public static decimal RoundConsumption(decimal quantity)
    {
        if (quantity <= 0 || quantity > MaximumQuantity) throw new BusinessRuleException(ErrorCode.KitchenOrderRecipeInvalid);
        return decimal.Ceiling(quantity * 1000000m) / 1000000m;
    }

    /// <summary>Qalığı mənfiyə və ya sütun həddindən yuxarı çıxarmır; stok bərpa olunanda əvvəlki minimum bildirişini sıfırlayır.</summary>
    public static void ApplyChange(InventoryItem item, decimal change)
    {
        var after = checked(item.QuantityOnHand + change);
        if (after < 0) throw new BusinessRuleException(ErrorCode.InventoryStockCannotBeNegative);
        if (after > MaximumQuantity) throw new BusinessRuleException(ErrorCode.InventoryStockQuantityInvalid);
        item.QuantityOnHand = after;
        if (after > item.LowStockThreshold) item.LastLowStockNotifiedAt = null;
    }
}
