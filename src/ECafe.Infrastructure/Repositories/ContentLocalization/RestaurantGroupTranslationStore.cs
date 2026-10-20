using ECafe.Application.DTOs.ContentLocalization;
using ECafe.Application.Mappings;
using ECafe.Application.Repositories.ContentLocalization;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace ECafe.Infrastructure.Repositories.ContentLocalization;

public sealed class RestaurantGroupTranslationStore(ECafeDbContext context)
    : ContentTranslationStore<RestaurantGroupTranslation>(context, nameof(RestaurantGroupTranslation.RestaurantGroupId),
        row => new ContentTranslationRow(row.RestaurantGroupId, row.Language.Code, row.Revision,
            new ContentTranslationValue(row.Name, null, null, null, null)))
{
    public override ContentEntityType Type => ContentEntityType.RestaurantGroup;

    /// <summary>Silinməmiş əsas məzmunu və aidiyyətini SQL proyeksiyası ilə oxuyur; biznes sahələrini dəyişmir.</summary>
    public override IQueryable<ContentTranslationSource> SourceQuery(int id)
        => Context.RestaurantGroups.AsNoTracking().Where(row => row.Id == id && !row.IsDeleted)
            .Select(row => new ContentTranslationSource(row.Id, null,
                new ContentTranslationValue(row.Name, null, null, null, null)));

    /// <summary>Yoxlanmış entity və dil composite açarı üçün tipli tərcümə sətri yaradır.</summary>
    protected override RestaurantGroupTranslation CreateRow(int id, int languageId)
        => new() { RestaurantGroupId = id, LanguageId = languageId };

    /// <summary>Yalnız bu məzmun növünə aid tərcümə sahələrini izlənən sətrə köçürür.</summary>
    protected override void ApplyValue(RestaurantGroupTranslation row, ContentTranslationValue value)
        => ContentTranslationMapper.Copy(value, row);
}
