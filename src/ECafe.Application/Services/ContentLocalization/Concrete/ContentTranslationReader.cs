using System.Globalization;
using ECafe.Application.Mappings;
using ECafe.Application.Repositories.ContentLocalization;
using ECafe.Application.Services.ContentLocalization.Abstract;
using ECafe.Domain.Policies;

namespace ECafe.Application.Services.ContentLocalization.Concrete;

public sealed class ContentTranslationReader(IContentTranslationQuery repository) : IContentTranslationReader
{
    /// <summary>Seçilmiş dilin mətnlərini entity növünə görə toplu oxuyur; boş və çatışmayan sahələr əsas DTO mətnində qalır.</summary>
    public async Task LocalizeAsync(object response, CancellationToken cancellationToken, ContentReadContext? context = null)
    {
        var language = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        if (language == TranslationPolicy.DefaultLanguage || !TranslationPolicy.Languages.Contains(language)) return;
        var bindings = ContentTranslationReadMapper.Collect(response, context?.RestaurantId, context?.UnitLookup ?? false);
        foreach (var group in bindings.GroupBy(binding => binding.Type))
        {
            var ids = group.Select(binding => binding.Id).Distinct().ToArray();
            var rows = await repository.GetRowsAsync(group.Key, ids, language, cancellationToken);
            var values = rows.ToDictionary(row => row.Id, row => row.Value);
            foreach (var binding in group)
                if (values.TryGetValue(binding.Id, out var value)) ContentTranslationReadMapper.Apply(binding, value);
        }
    }
}
