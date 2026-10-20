using ECafe.Domain.Entities;
using ECafe.Domain.Exceptions;

namespace ECafe.Domain.Policies;

public static class ContentTranslationCollectionPolicy
{
    /// <summary>Əlavə dilləri əvəz edir və ya mətnlərini təmizləyir; silinən dilin reviziyasını və AZ mənbəni qoruyur.</summary>
    public static void Apply<T>(ICollection<T> rows, IEnumerable<T>? replacements, T canonical, Action<T, T> copy)
        where T : EntityTranslation, new()
    {
        var incoming = replacements?.ToList();
        if (incoming != null && (incoming.Count > TranslationPolicy.Languages.Count - 1 ||
            incoming.Any(row => row == null || !TranslationPolicy.IsAdditionalLanguageId(row.LanguageId)) ||
            incoming.Select(row => row.LanguageId).Distinct().Count() != incoming.Count))
            throw new BusinessRuleException(ErrorCode.LocalizationInvalidEntry);
        if (rows.Any(row => row.Revision == long.MaxValue &&
            (row.LanguageId == TranslationPolicy.DefaultLanguageId || incoming != null)))
            throw new BusinessRuleException(ErrorCode.LocalizationDraftConflict);
        if (incoming != null)
        {
            foreach (var previous in rows.Where(row => row.LanguageId != TranslationPolicy.DefaultLanguageId && !incoming.Any(next => next.LanguageId == row.LanguageId)).ToList())
            {
                copy(new T(), previous);
                previous.Revision = checked(previous.Revision + 1);
            }
            foreach (var row in incoming) Upsert(rows, row, copy);
        }
        canonical.LanguageId = TranslationPolicy.DefaultLanguageId;
        Upsert(rows, canonical, copy);
    }

    /// <summary>Composite açarı qoruyur; sahələri tipli mapper ilə köçürüb reviziyanı artırır.</summary>
    private static void Upsert<T>(ICollection<T> rows, T incoming, Action<T, T> copy) where T : EntityTranslation
    {
        var existing = rows.SingleOrDefault(row => row.LanguageId == incoming.LanguageId);
        if (existing == null) { rows.Add(incoming); return; }
        copy(incoming, existing);
        existing.Revision = checked(existing.Revision + 1);
    }
}
