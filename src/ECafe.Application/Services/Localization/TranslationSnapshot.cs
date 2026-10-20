using System.Collections.Frozen;
using ECafe.Application.DTOs.Localization;
using ECafe.Domain.Policies;

namespace ECafe.Application.Services.Localization;

public sealed class TranslationSnapshot
{
    public string Version { get; }
    public IReadOnlyDictionary<string, TranslationText> Entries { get; }
    private readonly IReadOnlyDictionary<string, TranslationDictionaryResponse> _dictionaries;

    /// <summary>Dərc edilmiş lüğəti dəyişdirilməz edir və üç dilin hazır görünüşünü bir dəfə qurur; keşdə şəxsi məlumat saxlamır.</summary>
    public TranslationSnapshot(string version, IReadOnlyDictionary<string, TranslationText> entries)
    {
        Version = version;
        Entries = entries.ToFrozenDictionary(StringComparer.Ordinal);
        _dictionaries = TranslationPolicy.Languages.ToFrozenDictionary(language => language, language =>
            new TranslationDictionaryResponse(language, version, TranslationPolicy.DefaultLanguage,
                entries.ToFrozenDictionary(entry => entry.Key, entry => TextWithFallback(entry.Value, language), StringComparer.Ordinal)), StringComparer.Ordinal);
    }

    /// <summary>Hazır dil lüğətini əlavə DB sorğusu və dəyişdirilə bilən kolleksiya yaratmadan qaytarır.</summary>
    public TranslationDictionaryResponse ForLanguage(string language) => _dictionaries[TranslationPolicy.NormalizeLanguage(language)];

    /// <summary>Enum ilə seçilmiş dilin boş mətnini əsas AZ dəyərinə qaytarır; keş snapshot-ını dəyişmir.</summary>
    private static string TextWithFallback(TranslationText text, string language)
    {
        var translated = TranslationPolicy.TextFor(text, TranslationPolicy.ParseLanguage(language));
        return string.IsNullOrWhiteSpace(translated) ? text.Az : translated;
    }
}
