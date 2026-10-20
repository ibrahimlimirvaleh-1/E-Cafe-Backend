using ECafe.Application.DTOs.ContentLocalization;
using ECafe.Application.Mappings;
using ECafe.Application.Repositories.ContentLocalization;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace ECafe.Infrastructure.Repositories.ContentLocalization;

public sealed class ItemTranslationStore(ECafeDbContext context)
    : ContentTranslationStore<ItemTranslation>(context, nameof(ItemTranslation.ItemId),
        row => new ContentTranslationRow(row.ItemId, row.Language.Code, row.Revision,
            new ContentTranslationValue(row.Name, row.Description, null, null, row.UnavailableReason)))
{
    public override ContentEntityType Type => ContentEntityType.Item;

    /// <summary>Silinməmiş əsas məzmunu və aidiyyətini SQL proyeksiyası ilə oxuyur; biznes sahələrini dəyişmir.</summary>
    public override IQueryable<ContentTranslationSource> SourceQuery(int id)
        => Context.Items.AsNoTracking().Where(row => row.Id == id && !row.IsDeleted && !row.Restaurant.IsDeleted)
            .Select(row => new ContentTranslationSource(row.Id, row.RestaurantId,
                new ContentTranslationValue(row.Name, row.Description, null, null, row.UnavailableReason)));

    /// <summary>Yoxlanmış entity və dil composite açarı üçün tipli tərcümə sətri yaradır.</summary>
    protected override ItemTranslation CreateRow(int id, int languageId)
        => new() { ItemId = id, LanguageId = languageId };

    /// <summary>Yalnız bu məzmun növünə aid tərcümə sahələrini izlənən sətrə köçürür.</summary>
    protected override void ApplyValue(ItemTranslation row, ContentTranslationValue value)
        => ContentTranslationMapper.Copy(value, row);
}
