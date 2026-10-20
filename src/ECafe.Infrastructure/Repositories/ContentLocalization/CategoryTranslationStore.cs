using ECafe.Application.DTOs.ContentLocalization;
using ECafe.Application.Mappings;
using ECafe.Application.Repositories.ContentLocalization;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace ECafe.Infrastructure.Repositories.ContentLocalization;

public sealed class CategoryTranslationStore(ECafeDbContext context)
    : ContentTranslationStore<CategoryTranslation>(context, nameof(CategoryTranslation.CategoryId),
        row => new ContentTranslationRow(row.CategoryId, row.Language.Code, row.Revision,
            new ContentTranslationValue(row.Name, null, null)))
{
    public override ContentEntityType Type => ContentEntityType.Category;

    /// <summary>Silinməmiş əsas məzmunu və aidiyyətini SQL proyeksiyası ilə oxuyur; biznes sahələrini dəyişmir.</summary>
    public override IQueryable<ContentTranslationSource> SourceQuery(int id)
        => Context.Categories.AsNoTracking().Where(row => row.Id == id && !row.IsDeleted && !row.Restaurant.IsDeleted)
            .Select(row => new ContentTranslationSource(row.Id, row.RestaurantId,
                new ContentTranslationValue(row.Name, null, null)));

    /// <summary>Yoxlanmış entity və dil composite açarı üçün tipli tərcümə sətri yaradır.</summary>
    protected override CategoryTranslation CreateRow(int id, int languageId)
        => new() { CategoryId = id, LanguageId = languageId };

    /// <summary>Yalnız bu məzmun növünə aid tərcümə sahələrini izlənən sətrə köçürür.</summary>
    protected override void ApplyValue(CategoryTranslation row, ContentTranslationValue value)
        => ContentTranslationMapper.Copy(value, row);
}
