using ECafe.Application.DTOs.ContentLocalization;
using ECafe.Application.Mappings;
using ECafe.Application.Repositories.ContentLocalization;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace ECafe.Infrastructure.Repositories.ContentLocalization;

public sealed class UnitTranslationStore(ECafeDbContext context)
    : ContentTranslationStore<UnitTranslation>(context, nameof(UnitTranslation.UnitId),
        row => new ContentTranslationRow(row.UnitId, row.Language.Code, row.Revision,
            new ContentTranslationValue(row.Name, null, null, null, null)))
{
    public override ContentEntityType Type => ContentEntityType.Unit;

    /// <summary>Silinməmiş əsas məzmunu və aidiyyətini SQL proyeksiyası ilə oxuyur; biznes sahələrini dəyişmir.</summary>
    public override IQueryable<ContentTranslationSource> SourceQuery(int id)
        => Context.Units.AsNoTracking().Where(row => row.Id == id && !row.IsDeleted)
            .Select(row => new ContentTranslationSource(row.Id, null,
                new ContentTranslationValue(row.Name, null, null, null, null)));

    /// <summary>Yoxlanmış entity və dil composite açarı üçün tipli tərcümə sətri yaradır.</summary>
    protected override UnitTranslation CreateRow(int id, int languageId)
        => new() { UnitId = id, LanguageId = languageId };

    /// <summary>Yalnız bu məzmun növünə aid tərcümə sahələrini izlənən sətrə köçürür.</summary>
    protected override void ApplyValue(UnitTranslation row, ContentTranslationValue value)
        => ContentTranslationMapper.Copy(value, row);
}
