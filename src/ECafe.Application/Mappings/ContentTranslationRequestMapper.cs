using ECafe.Application.DTOs.Category;
using ECafe.Application.DTOs.InventoryItem;
using ECafe.Application.DTOs.Item;
using ECafe.Application.DTOs.Table;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using ECafe.Domain.Policies;

namespace ECafe.Application.Mappings;

public static class ContentTranslationRequestMapper
{
    /// <summary>Məhsul sorğusunu domenin normallaşdırdığı mətnlərdən yeni tərcümə sətirlərinə çevirir; izlənən məhsulu dəyişmir.</summary>
    public static List<ItemTranslation>? MapItem(int itemId, IReadOnlyCollection<ItemTranslationRequest>? requests)
        => Map(requests, request =>
        {
            var languageId = ContentTranslationPolicy.EditableLanguageId(request.LanguageCode);
            var fields = ContentTranslationPolicy.Normalize(ContentEntityType.Item,
                new(request.Name, request.Description, UnavailableReason: request.UnavailableReason), requireName: true);
            return new ItemTranslation
            {
                ItemId = itemId, LanguageId = languageId,
                Name = fields.Name, Description = fields.Description, UnavailableReason = fields.UnavailableReason
            };
        });

    /// <summary>Kateqoriya sorğusunu ayrıca tərcümə sətirlərinə çevirir; kolleksiyanın əvəzlənməsini icra etmir.</summary>
    public static List<CategoryTranslation>? MapCategory(int categoryId, IReadOnlyCollection<CategoryTranslationRequest>? requests)
        => Map(requests, request => new CategoryTranslation
        {
            CategoryId = categoryId, LanguageId = ContentTranslationPolicy.EditableLanguageId(request.LanguageCode),
            Name = ContentTranslationPolicy.Normalize(ContentEntityType.Category, new(request.Name), requireName: true).Name
        });

    /// <summary>Masanın göstərilən adını yeni tərcümə sətrinə çevirir; masa nömrəsinə və əlaqələrinə toxunmur.</summary>
    public static List<TableTranslation>? MapTable(int tableId, IReadOnlyCollection<TableTranslationRequest>? requests)
        => Map(requests, request => new TableTranslation
        {
            TableId = tableId, LanguageId = ContentTranslationPolicy.EditableLanguageId(request.LanguageCode),
            Name = ContentTranslationPolicy.Normalize(ContentEntityType.Table, new(request.Name), requireName: true).Name
        });

    /// <summary>Anbar məhsulunun tərcümə sorğusunu stok miqdarına toxunmadan yeni sətirlərə çevirir.</summary>
    public static List<InventoryItemTranslation>? MapInventoryItem(int itemId, IReadOnlyCollection<InventoryItemTranslationRequest>? requests)
        => Map(requests, request => new InventoryItemTranslation
        {
            InventoryItemId = itemId, LanguageId = ContentTranslationPolicy.EditableLanguageId(request.LanguageCode),
            Name = ContentTranslationPolicy.Normalize(ContentEntityType.InventoryItem, new(request.Name), requireName: true).Name
        });

    /// <summary>Məhsulun əsas mətnlərini AZ sətrinin mənbə modeli kimi hazırlayır.</summary>
    public static ItemTranslation Canonical(Item item) => new()
    {
        ItemId = item.Id, LanguageId = TranslationPolicy.DefaultLanguageId, Name = item.Name, Description = item.Description, UnavailableReason = item.UnavailableReason
    };

    /// <summary>Kateqoriyanın əsas adını AZ sətrinin mənbə modeli kimi hazırlayır.</summary>
    public static CategoryTranslation Canonical(Category category) => new() { CategoryId = category.Id, LanguageId = TranslationPolicy.DefaultLanguageId, Name = category.Name };

    /// <summary>Masanın əsas adını AZ sətrinin mənbə modeli kimi hazırlayır; boş əsas ad saxlanılır.</summary>
    public static TableTranslation Canonical(Table table) => new() { TableId = table.Id, LanguageId = TranslationPolicy.DefaultLanguageId, Name = table.Name };

    /// <summary>Anbar məhsulunun əsas adını AZ sətrinin mənbə modeli kimi hazırlayır.</summary>
    public static InventoryItemTranslation Canonical(InventoryItem item) => new() { InventoryItemId = item.Id, LanguageId = TranslationPolicy.DefaultLanguageId, Name = item.Name };

    /// <summary>Masa surəti üçün əlavə dillərin müstəqil sətirlərini yaradır; mənbə ilə obyekt paylaşmır.</summary>
    public static List<TableTranslation> MapTableCopy(Table source, int targetId)
        => source.Translations.Where(row => row.LanguageId != TranslationPolicy.DefaultLanguageId).Select(row => new TableTranslation
        {
            TableId = targetId, LanguageId = row.LanguageId, Name = row.Name
        }).ToList();

    /// <summary>Sorğu verilmədikdə null-u qoruyur və bütün sətirləri izlənən kolleksiyaya toxunmadan əvvəl materiallaşdırır.</summary>
    private static List<TEntity>? Map<TRequest, TEntity>(IReadOnlyCollection<TRequest>? requests, Func<TRequest, TEntity> map)
    {
        if (requests is null) return null;
        ContentTranslationPolicy.RequireAdditionalLanguageCount(requests.Count);
        return requests.Select(request => request is null
            ? throw new BusinessRuleException(ErrorCode.LocalizationInvalidEntry)
            : map(request)).ToList();
    }
}
