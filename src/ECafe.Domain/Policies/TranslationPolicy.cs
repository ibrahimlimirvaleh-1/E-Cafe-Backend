using System.Text.RegularExpressions;
using ECafe.Domain.Exceptions;
using ECafe.Domain.Enums;

namespace ECafe.Domain.Policies;

public static partial class TranslationPolicy
{
    public const LanguageCode DefaultLanguageCode = LanguageCode.Az;
    public const int DefaultLanguageId = (int)DefaultLanguageCode;
    private static readonly IReadOnlyDictionary<string, LanguageCode> Codes = Enum.GetValues<LanguageCode>()
        .ToDictionary(language => language.ToString().ToLowerInvariant(), StringComparer.Ordinal);
    public static string DefaultLanguage { get; } = Code(DefaultLanguageCode);
    public static IReadOnlyList<string> Languages { get; } = Array.AsReadOnly(Enum.GetValues<LanguageCode>().Select(Code).ToArray());
    public const int MaxTextLength = 16000;
    public const int MaxBatchSize = 250;
    public const int MaxCatalogBytes = 12 * 1024 * 1024;

    /// <summary>Yalnız dəstəklənən dil kodunu qəbul edir; regional kodu əsas dilə çevirir, naməlum dili Azərbaycan dilinə qaytarır.</summary>
    public static string NormalizeLanguage(string? value)
    {
        var language = value?.Trim().Split('-', '_')[0].ToLowerInvariant();
        return language != null && Codes.ContainsKey(language) ? language : DefaultLanguage;
    }

    /// <summary>Sabit enum dəyərini API və bazanın dəyişməyən iki hərfli dil koduna çevirir.</summary>
    public static string Code(LanguageCode language)
        => Enum.IsDefined(language) ? language.ToString().ToLowerInvariant() : throw new ArgumentOutOfRangeException(nameof(language));

    /// <summary>Yazı sorğusunda yalnız dəqiq dəstəklənən dil kodunu qəbul edir; rəqəm və regional alias saxlamır.</summary>
    public static LanguageCode ParseLanguage(string? value)
        => value != null && Codes.TryGetValue(value, out var language)
            ? language : throw new BusinessRuleException(ErrorCode.LocalizationInvalidLanguage);

    /// <summary>Sorğunun dilini normallaşdırıb enum-da sabitlənmiş dil FK-sını qaytarır.</summary>
    public static int LanguageId(string? preference) => (int)ParseLanguage(NormalizeLanguage(preference));

    /// <summary>Əlavə tərcümə sətrinin yalnız dəstəklənən, əsas olmayan dilə aid olduğunu müəyyən edir.</summary>
    public static bool IsAdditionalLanguageId(int id)
        => Enum.IsDefined((LanguageCode)id) && id != DefaultLanguageId;

    /// <summary>Tipli dil seçiminə uyğun lüğət mətnini qaytarır; naməlum enum dəyəri səssiz fallback etmir.</summary>
    public static string TextFor(TranslationText text, LanguageCode language) => language switch
    {
        LanguageCode.Az => text.Az,
        LanguageCode.En => text.En,
        LanguageCode.Ru => text.Ru,
        _ => throw new ArgumentOutOfRangeException(nameof(language))
    };

    /// <summary>Admin və dil seçimi yazılarında naməlum və sərhədsiz dil kodlarının bazaya düşməsini dayandırır.</summary>
    public static void ValidateLanguage(string? value)
    {
        _ = ParseLanguage(value);
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
