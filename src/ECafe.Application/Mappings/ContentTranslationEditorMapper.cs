using ECafe.Application.DTOs.ContentLocalization;
using ECafe.Application.Repositories.ContentLocalization;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using ECafe.Domain.Policies;

namespace ECafe.Application.Mappings;

public static class ContentTranslationEditorMapper
{
    private static readonly IReadOnlyDictionary<string, ContentEntityType> Types = new Dictionary<string, ContentEntityType>(StringComparer.Ordinal)
    {
        ["categories"] = ContentEntityType.Category,
        ["items"] = ContentEntityType.Item,
        ["tables"] = ContentEntityType.Table,
        ["inventory-items"] = ContentEntityType.InventoryItem,
        ["units"] = ContentEntityType.Unit
    };

    /// <summary>Yalnız açıq API müqaviləsində qeydiyyata alınmış məzmun adını domen növünə çevirir.</summary>
    public static ContentEntityType ParseType(string type)
        => Types.TryGetValue(type, out var result) ? result : throw new BusinessRuleException(ErrorCode.LocalizationInvalidEntry);

    /// <summary>Domenin yoxlanmış mətnlərini redaktorun sabit API dəyərinə köçürür.</summary>
    public static ContentTranslationValue Value(ContentTranslationFields fields)
        => new(fields.Name, fields.Description, fields.UnavailableReason);

    /// <summary>Xam AZ mənbə ilə əlavə dilləri fallback etmədən redaktor cavabına köçürür.</summary>
    public static ContentTranslationsResponse Response(string entityType, ContentTranslationSource source,
        IReadOnlyList<ContentTranslationRow> rows, IReadOnlyList<ContentLanguage> languages)
    {
        var entries = languages.Where(language => language.IsActive && TranslationPolicy.Languages.Contains(language.Code))
            .OrderBy(language => language.Id).Select(language =>
            {
                var row = rows.SingleOrDefault(item => item.LanguageCode == language.Code);
                var value = language.Code == TranslationPolicy.DefaultLanguage ? source.Canonical : row?.Value ?? new ContentTranslationValue();
                return new ContentTranslationEntry(language.Code, value.Name, value.Description,
                    value.UnavailableReason, row?.Revision ?? 0);
            }).ToList();
        return new(entityType, source.Id, source.RestaurantId, entries);
    }
}
