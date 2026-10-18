using System.Text.RegularExpressions;
using ECafe.Domain.Exceptions;

namespace ECafe.Domain.Policies;

public sealed record TranslationText(string Az, string En, string Ru, string? Source = null);

public static partial class TranslationPolicy
{
    public const string DefaultLanguage = "az";
    public static readonly IReadOnlyList<string> Languages = Array.AsReadOnly(new[] { "az", "en", "ru" });
    public const int MaxTextLength = 16000;
    public const int MaxBatchSize = 250;
    public const int MaxCatalogBytes = 12 * 1024 * 1024;

    /// <summary>Yalnız dəstəklənən dil kodunu qəbul edir; regional kodu əsas dilə çevirir, naməlum dili Azərbaycan dilinə qaytarır.</summary>
    public static string NormalizeLanguage(string? value)
    {
        var language = value?.Trim().Split('-', '_')[0].ToLowerInvariant();
        return language is "en" or "ru" ? language : DefaultLanguage;
    }

    /// <summary>Admin və dil seçimi yazılarında naməlum və sərhədsiz dil kodlarının bazaya düşməsini dayandırır.</summary>
    public static void ValidateLanguage(string? value)
    {
        if (value is null || !Languages.Contains(value)) throw new BusinessRuleException(ErrorCode.LocalizationInvalidLanguage);
    }

    /// <summary>Tərcümə mətnini ölçü, təhlükəli markup və dəyişənlərin uyğunluğu üzrə yoxlayır; kod və HTML icrasına icazə vermir.</summary>
    public static void ValidateText(TranslationText value)
    {
        if (value is null) throw new BusinessRuleException(ErrorCode.LocalizationInvalidEntry);
        var texts = new[] { value.Az, value.En, value.Ru };
        if (texts.Any(text => string.IsNullOrWhiteSpace(text) || text.Length > MaxTextLength || Markup().IsMatch(text) ||
            text.Any(character => char.IsControl(character) && character is not '\n' and not '\r' and not '\t')))
            throw new BusinessRuleException(ErrorCode.LocalizationInvalidEntry);
        var expected = Placeholders(value.Az);
        if (!expected.SetEquals(Placeholders(value.En)) || !expected.SetEquals(Placeholders(value.Ru)))
            throw new BusinessRuleException(ErrorCode.LocalizationPlaceholderMismatch);
    }

    /// <summary>Tərcümə dəyişənlərini düz adlar kimi çıxarır; həm backend {name}, həm frontend {{name}} formatını tanıyır.</summary>
    public static HashSet<string> Placeholders(string value)
        => PlaceholderPattern().Matches(value).Select(match => match.Groups[1].Value).ToHashSet(StringComparer.Ordinal);

    /// <summary>Markup-a oxşayan teqləri qəbul etmir; tərcümə yalnız mətn kimi yayımlanır.</summary>
    [GeneratedRegex(@"<\s*[/!a-zA-Z]", RegexOptions.CultureInvariant)]
    private static partial Regex Markup();
    /// <summary>Adlandırılmış tərcümə dəyişənlərini sərhədli şəkildə tanıyır; format ifadəsini icra etmir.</summary>
    [GeneratedRegex(@"\{\{?\s*([a-zA-Z_][a-zA-Z0-9_.]*)\s*\}\}?", RegexOptions.CultureInvariant)]
    private static partial Regex PlaceholderPattern();
}
