using ECafe.Application.DTOs.ContentLocalization;
using ECafe.Application.Mappings;
using ECafe.Application.Repositories.ContentLocalization;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace ECafe.Infrastructure.Repositories.ContentLocalization;

public sealed class TableTranslationStore(ECafeDbContext context)
    : ContentTranslationStore<TableTranslation>(context, nameof(TableTranslation.TableId),
        row => new ContentTranslationRow(row.TableId, row.Language.Code, row.Revision,
            new ContentTranslationValue(row.Name, null, null, null, null)))
{
    public override ContentEntityType Type => ContentEntityType.Table;

    /// <summary>Silinməmiş əsas məzmunu və aidiyyətini SQL proyeksiyası ilə oxuyur; biznes sahələrini dəyişmir.</summary>
    public override IQueryable<ContentTranslationSource> SourceQuery(int id)
        => Context.Tables.AsNoTracking().Where(row => row.Id == id && !row.IsDeleted && !row.Restaurant.IsDeleted)
            .Select(row => new ContentTranslationSource(row.Id, row.RestaurantId,
                new ContentTranslationValue(row.Name, null, null, null, null)));

    /// <summary>Yoxlanmış entity və dil composite açarı üçün tipli tərcümə sətri yaradır.</summary>
    protected override TableTranslation CreateRow(int id, int languageId)
        => new() { TableId = id, LanguageId = languageId };

    /// <summary>Yalnız bu məzmun növünə aid tərcümə sahələrini izlənən sətrə köçürür.</summary>
    protected override void ApplyValue(TableTranslation row, ContentTranslationValue value)
        => ContentTranslationMapper.Copy(value, row);
}
