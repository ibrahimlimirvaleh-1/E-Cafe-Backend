using System.Text.Json;
using ECafe.Domain.Policies;

namespace ECafe.Application.Services.Localization;

public static class TranslationJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    /// <summary>Yalnız məlum lüğət strukturunu oxuyur; sərbəst obyekt və kod deserializasiyası etmir.</summary>
    public static Dictionary<string, TranslationText> Read(string value)
        => JsonSerializer.Deserialize<Dictionary<string, TranslationText>>(value, Options) ?? new(StringComparer.Ordinal);
}
