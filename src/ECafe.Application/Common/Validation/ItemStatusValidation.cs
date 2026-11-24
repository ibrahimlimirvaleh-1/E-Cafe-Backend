using ECafe.Domain.Enums;

namespace ECafe.Application.Common.Validation;

public static class ItemStatusValidation
{
    /// <summary>Məhsul yaradılarkən və redaktə edilərkən yalnız məhsul status tipinə aid birləşdirilmiş ID-ləri qəbul edir.</summary>
    public static bool IsValid(int statusId)
        => Enum.GetValues<ItemStatus>()
            .Select(status => ((int)StatusType.ItemStatus * 1000) + Convert.ToInt32(status))
            .Contains(statusId);
}
