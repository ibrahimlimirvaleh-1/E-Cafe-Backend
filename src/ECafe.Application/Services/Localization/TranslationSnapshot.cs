using System.Collections.Frozen;
using System.Text.Json;
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
                entries.ToFrozenDictionary(entry => entry.Key, entry => language switch
                {
                    "en" => string.IsNullOrWhiteSpace(entry.Value.En) ? entry.Value.Az : entry.Value.En,
                    "ru" => string.IsNullOrWhiteSpace(entry.Value.Ru) ? entry.Value.Az : entry.Value.Ru,
                    _ => entry.Value.Az
                }, StringComparer.Ordinal)), StringComparer.Ordinal);
    }

    /// <summary>Hazır dil lüğətini əlavə DB sorğusu və dəyişdirilə bilən kolleksiya yaratmadan qaytarır.</summary>
    public TranslationDictionaryResponse ForLanguage(string language) => _dictionaries[TranslationPolicy.NormalizeLanguage(language)];
}

public static class TranslationJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
    /// <summary>Yalnız tərcümə kataloqunun məlum mətn strukturunu oxuyur; obyekt tipi və kod deserializasiyası etmir.</summary>
    public static Dictionary<string, TranslationText> Read(string value)
        => JsonSerializer.Deserialize<Dictionary<string, TranslationText>>(value, Options) ?? new(StringComparer.Ordinal);
}
