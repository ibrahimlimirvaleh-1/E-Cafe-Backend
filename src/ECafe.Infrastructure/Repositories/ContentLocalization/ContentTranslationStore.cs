using System.Linq.Expressions;
using ECafe.Application.DTOs.ContentLocalization;
using ECafe.Application.Repositories.ContentLocalization;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace ECafe.Infrastructure.Repositories.ContentLocalization;

public abstract class ContentTranslationStore<TTranslation>(
    ECafeDbContext context,
    string ownerKey,
    Expression<Func<TTranslation, ContentTranslationRow>> projection) : IContentTranslationStore
    where TTranslation : EntityTranslation
{
    protected ECafeDbContext Context { get; } = context;
    public abstract ContentEntityType Type { get; }
    /// <summary>Uyğun entity-nin silinmə və restoran sərhədləri qorunan SQL mənbə proyeksiyasını verir.</summary>
    public abstract IQueryable<ContentTranslationSource> SourceQuery(int id);
    /// <summary>Entity və dil identifikatorları ilə yalnız yeni tipli əlaqə sətrini yaradır.</summary>
    protected abstract TTranslation CreateRow(int id, int languageId);
    /// <summary>Yoxlanmış mətnlərin köçürülməsini uyğun tipli mapper-ə ötürür; saxlanma və reviziyanı idarə etmir.</summary>
    protected abstract void ApplyValue(TTranslation row, ContentTranslationValue value);

    /// <summary>Uyğun composite açarları və dili SQL daxilində süzərək yalnız cavab sahələrini proyeksiya edir.</summary>
    public IQueryable<ContentTranslationRow> RowsQuery(IReadOnlyCollection<int> ids, string? language)
        => Context.Set<TTranslation>().AsNoTracking()
            .Where(row => ids.Contains(EF.Property<int>(row, ownerKey)) && (language == null || row.Language.Code == language))
            .Select(projection);

    /// <summary>İzlənən composite sətrin reviziyasını təkrar yoxlayır; mövcud obyektin ikinci nüsxəsini yaratmadan yeniləyir.</summary>
    public async Task SaveAsync(int id, int languageId, ContentTranslationValue value, long revision, CancellationToken cancellationToken)
    {
        var row = await Context.Set<TTranslation>().FindAsync([id, languageId], cancellationToken);
        if ((row?.Revision ?? 0) != revision - 1) throw new DbUpdateConcurrencyException();
        if (row == null)
        {
            row = CreateRow(id, languageId);
            Context.Add(row);
        }
        ApplyValue(row, value);
        row.Revision = revision;
        await Context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Mətnləri təmizləyib reviziyanı artırır; silinib yenidən yaradılan dilə köhnə redaktorun yazmasını bloklayır.</summary>
    public async Task DeleteAsync(int id, int languageId, long expectedRevision, CancellationToken cancellationToken)
    {
        var row = await Context.Set<TTranslation>().FindAsync([id, languageId], cancellationToken);
        if ((row?.Revision ?? 0) != expectedRevision) throw new DbUpdateConcurrencyException();
        if (row != null)
        {
            ApplyValue(row, new ContentTranslationValue());
            row.Revision = checked(row.Revision + 1);
        }
        await Context.SaveChangesAsync(cancellationToken);
    }
}
