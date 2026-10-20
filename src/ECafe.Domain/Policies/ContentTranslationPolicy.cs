using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;

namespace ECafe.Domain.Policies;

public static class ContentTranslationPolicy
{
    private static readonly IReadOnlyDictionary<ContentEntityType, ContentTranslationRules> Rules =
        new Dictionary<ContentEntityType, ContentTranslationRules>
        {
            [ContentEntityType.Restaurant] = new(200, PermissionCode.EditRestaurantInfo, AllowsLocation: true),
            [ContentEntityType.RestaurantGroup] = new(100, PermissionCode.ManageRestaurants),
            [ContentEntityType.Category] = new(100, PermissionCode.ManageCatalog),
            [ContentEntityType.Item] = new(150, PermissionCode.ManageCatalog, AllowsDescription: true),
            [ContentEntityType.Table] = new(50, PermissionCode.ManageTables),
            [ContentEntityType.InventoryItem] = new(150, PermissionCode.ManageInventory),
            [ContentEntityType.Unit] = new(50, PermissionCode.ManageInventory)
        };

    /// <summary>Sahə hədlərini və mənbə yazı icazəsini eyni domen qaydasından seçir.</summary>
    public static ContentTranslationRules For(ContentEntityType type)
        => Rules.TryGetValue(type, out var rules) ? rules : throw new BusinessRuleException(ErrorCode.LocalizationInvalidEntry);

    /// <summary>Yalnız əlavə dilin FK-sını həll edir; AZ əsas CRUD sahələrindən sinxronlaşdırılır.</summary>
    public static int EditableLanguageId(string? code)
    {
        var language = TranslationPolicy.ParseLanguage(code);
        return language != TranslationPolicy.DefaultLanguageCode
            ? (int)language : throw new BusinessRuleException(ErrorCode.LocalizationInvalidLanguage);
    }

    /// <summary>Böyük tərcümə siyahısını mətnləri emal etməzdən əvvəl rədd edir.</summary>
    public static void RequireAdditionalLanguageCount(int count)
    {
        if (count > TranslationPolicy.Languages.Count - 1)
            throw new BusinessRuleException(ErrorCode.LocalizationInvalidEntry);
    }

    /// <summary>Uyğun olmayan sahələri, boş məzmunu və təhlükəli mətni kolleksiyaya tətbiq edilməzdən əvvəl rədd edir.</summary>
    public static ContentTranslationFields Normalize(ContentEntityType type, ContentTranslationFields fields,
        bool requireName = false, bool trimBeforeLengthCheck = false)
    {
        var rules = For(type);
        if (!rules.AllowsLocation && (fields.Location != null || fields.BranchName != null) ||
            !rules.AllowsDescription && (fields.Description != null || fields.UnavailableReason != null))
            throw new BusinessRuleException(ErrorCode.LocalizationInvalidEntry);
        var value = new ContentTranslationFields(Text(fields.Name, rules.NameLength, trimBeforeLengthCheck),
            Text(fields.Description, 500, trimBeforeLengthCheck), Text(fields.Location, 1000, trimBeforeLengthCheck),
            Text(fields.BranchName, 100, trimBeforeLengthCheck), Text(fields.UnavailableReason, 500, trimBeforeLengthCheck));
        if (value == new ContentTranslationFields() || requireName && value.Name == null)
            throw new BusinessRuleException(ErrorCode.LocalizationInvalidEntry);
        return value;
    }

    /// <summary>Redaktor reviziyasının açıq, sərhədli və artım üçün təhlükəsiz olmasını tələb edir.</summary>
    public static void RequireRevision(long? revision)
    {
        if (!revision.HasValue || revision.Value < 0 || revision.Value == long.MaxValue)
            throw new BusinessRuleException(ErrorCode.LocalizationInvalidEntry);
    }

    /// <summary>Köhnə ekranın daha yeni tərcüməni səssiz əvəz etməsinə və silməsinə mane olur.</summary>
    public static void RequireCurrentRevision(long currentRevision, long expectedRevision)
    {
        if (currentRevision != expectedRevision) throw new BusinessRuleException(ErrorCode.LocalizationDraftConflict);
    }

    /// <summary>Mətn ölçüsünü və təhlükəsizliyini yoxlayır; boş əlavə sahə fallback üçün null olur.</summary>
    private static string? Text(string? value, int length, bool trimBeforeLengthCheck)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var candidate = trimBeforeLengthCheck ? value.Trim() : value;
        if (candidate.Length > length) throw new BusinessRuleException(ErrorCode.LocalizationInvalidEntry);
        TranslationPolicy.ValidateText(new(candidate, candidate, candidate));
        return candidate.Trim();
    }
}
