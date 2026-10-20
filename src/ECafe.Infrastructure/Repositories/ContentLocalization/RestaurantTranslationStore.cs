using ECafe.Application.DTOs.ContentLocalization;
using ECafe.Application.Mappings;
using ECafe.Application.Repositories.ContentLocalization;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace ECafe.Infrastructure.Repositories.ContentLocalization;

public sealed class RestaurantTranslationStore(ECafeDbContext context)
    : ContentTranslationStore<RestaurantTranslation>(context, nameof(RestaurantTranslation.RestaurantId),
        row => new ContentTranslationRow(row.RestaurantId, row.Language.Code, row.Revision,
            new ContentTranslationValue(row.Name, null, row.Location, row.BranchName, null)))
{
    public override ContentEntityType Type => ContentEntityType.Restaurant;

    /// <summary>Silinməmiş əsas məzmunu və aidiyyətini SQL proyeksiyası ilə oxuyur; biznes sahələrini dəyişmir.</summary>
    public override IQueryable<ContentTranslationSource> SourceQuery(int id)
        => Context.Restaurants.AsNoTracking().Where(row => row.Id == id && !row.IsDeleted)
            .Select(row => new ContentTranslationSource(row.Id, row.Id,
                new ContentTranslationValue(row.Name, null, row.Location, row.BranchName, null)));

    /// <summary>Yoxlanmış entity və dil composite açarı üçün tipli tərcümə sətri yaradır.</summary>
    protected override RestaurantTranslation CreateRow(int id, int languageId)
        => new() { RestaurantId = id, LanguageId = languageId };

    /// <summary>Yalnız bu məzmun növünə aid tərcümə sahələrini izlənən sətrə köçürür.</summary>
    protected override void ApplyValue(RestaurantTranslation row, ContentTranslationValue value)
        => ContentTranslationMapper.Copy(value, row);
}
