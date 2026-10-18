using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ECafe.Application.Common.Errors;
using ECafe.Domain.Policies;

namespace ECafe.Application.Services.Localization;

public sealed class TranslationBootstrap
{
    public TranslationSnapshot Snapshot { get; }
    public TranslationSourceMatcher SourceMatcher { get; }
    public TranslationSourceMatcher NotificationSourceMatcher { get; }

    /// <summary>Build ilə gələn təhlükəsiz ehtiyat kataloqunu bir dəfə oxuyur; DB əlçatmaz olanda və ilk yayımdan əvvəl mətnlərin itməsinə imkan vermir.</summary>
    public TranslationBootstrap()
    {
        var entries = ErrorMessageProvider.DefaultMessages.ToDictionary(pair => $"api.error.{pair.Key}",
            pair => new TranslationText(pair.Value, pair.Value, pair.Value), StringComparer.Ordinal);
        var assembly = typeof(TranslationBootstrap).Assembly;
        var ui = TranslationPolicy.Languages.ToDictionary(language => language, _ => new Dictionary<string, string>(StringComparer.Ordinal));
        foreach (var name in assembly.GetManifestResourceNames().Where(name => name.Contains("Localization.Resources.") && name.EndsWith(".json", StringComparison.Ordinal)))
        {
            using var stream = assembly.GetManifestResourceStream(name)!;
            if (name.EndsWith(".server.json", StringComparison.Ordinal))
            {
                foreach (var pair in JsonSerializer.Deserialize<Dictionary<string, TranslationText>>(stream, TranslationJson.Options)!) entries[pair.Key] = pair.Value;
                continue;
            }
            var language = TranslationPolicy.Languages.FirstOrDefault(language => name.EndsWith($".{language}.json", StringComparison.Ordinal));
            if (language == null) continue;
            var messages = JsonSerializer.Deserialize<Dictionary<string, string>>(stream)!;
            foreach (var pair in messages.Where(pair => pair.Key.StartsWith("ui.", StringComparison.Ordinal))) ui[language][pair.Key] = pair.Value;
        }
        foreach (var key in ui.Values.SelectMany(dictionary => dictionary.Keys).Distinct(StringComparer.Ordinal))
        {
            var az = ui["az"].GetValueOrDefault(key) ?? ui["en"].GetValueOrDefault(key) ?? ui["ru"][key];
            entries[key] = new(az, ui["en"].GetValueOrDefault(key, az), ui["ru"].GetValueOrDefault(key, az));
        }
        var sorted = entries.OrderBy(entry => entry.Key, StringComparer.Ordinal).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(sorted, TranslationJson.Options));
        Snapshot = new("bootstrap-" + Convert.ToHexString(SHA256.HashData(bytes))[..16], sorted);
        var sources = sorted.Where(entry => entry.Key.StartsWith("api.", StringComparison.Ordinal) || entry.Key.StartsWith("notification.", StringComparison.Ordinal))
            .Select(entry => new KeyValuePair<string, string>(entry.Key, entry.Value.Source ?? entry.Value.Az))
            .Concat(ErrorMessageProvider.DefaultMessages.Select(entry => new KeyValuePair<string, string>($"api.error.{entry.Key}", entry.Value))).ToArray();
        SourceMatcher = new TranslationSourceMatcher(sources);
        NotificationSourceMatcher = new TranslationSourceMatcher(sources.OrderByDescending(entry => entry.Key.StartsWith("notification.", StringComparison.Ordinal)));
    }
}
