using ECafe.Application.DTOs.ContentLocalization;
using ECafe.Application.Mappings;
using ECafe.Application.Repositories.ContentLocalization;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace ECafe.Infrastructure.Repositories.ContentLocalization;

public sealed class InventoryItemTranslationStore(ECafeDbContext context)
    : ContentTranslationStore<InventoryItemTranslation>(context, nameof(InventoryItemTranslation.InventoryItemId),
        row => new ContentTranslationRow(row.InventoryItemId, row.Language.Code, row.Revision,
            new ContentTranslationValue(row.Name, null, null)))
{
    public override ContentEntityType Type => ContentEntityType.InventoryItem;

    /// <summary>Silinməmiş əsas məzmunu və aidiyyətini SQL proyeksiyası ilə oxuyur; biznes sahələrini dəyişmir.</summary>
    public override IQueryable<ContentTranslationSource> SourceQuery(int id)
        => Context.InventoryItems.AsNoTracking().Where(row => row.Id == id && !row.IsDeleted && !row.Restaurant.IsDeleted)
            .Select(row => new ContentTranslationSource(row.Id, row.RestaurantId,
                new ContentTranslationValue(row.Name, null, null)));

    /// <summary>Yoxlanmış entity və dil composite açarı üçün tipli tərcümə sətri yaradır.</summary>
    protected override InventoryItemTranslation CreateRow(int id, int languageId)
        => new() { InventoryItemId = id, LanguageId = languageId };

    /// <summary>Yalnız bu məzmun növünə aid tərcümə sahələrini izlənən sətrə köçürür.</summary>
    protected override void ApplyValue(InventoryItemTranslation row, ContentTranslationValue value)
        => ContentTranslationMapper.Copy(value, row);
}
