using System.Globalization;
using System.Reflection;
using ECafe.Application.DTOs.Localization;
using ECafe.Application.Services.Localization.Abstract;
using Microsoft.AspNetCore.Http;

namespace ECafe.Application.Services.Localization.Concrete;

public sealed class TextLocalizer(IHttpContextAccessor accessor, TranslationBootstrap bootstrap) : ITextLocalizer
{
    public const string ContextKey = "ECafe.TranslationDictionary";
    /// <summary>Sorğunun yayımlanmış lüğətindən mətn seçir; mədəniyyətə görə rəqəmləri formatlayır, çatışmayan açarda verilmiş ehtiyat mətnini saxlayır.</summary>
    public string Get(string key, string fallback, object? parameters = null)
    {
        var dictionary = accessor.HttpContext?.Items[ContextKey] as TranslationDictionaryResponse
            ?? bootstrap.Snapshot.ForLanguage(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
        return Render(dictionary.Messages, key, fallback, parameters);
    }
    /// <summary>Yalnız kataloqda açıq qeydə alınmış sabit API mətnini dəyişir; restoran/müştəri məlumatını avtomatik tərcümə etmir.</summary>
    public string TranslateLiteral(string value)
    {
        var match = ResolveSource(value);
        return match == null ? value : Get(match.Key, value, match.Arguments);
    }
    /// <summary>Mövcud sistem mesajını versiyadan asılı olmayan build aliası ilə açara çevirir; naməlum istifadəçi mətnini olduğu kimi saxlayır.</summary>
    public TranslationMatch? ResolveSource(string value)
        => ResolveSource(value, bootstrap.SourceMatcher);

    /// <summary>Eyni söz statusda və bildirişdə olduqda bildirişin ayrıca redaktə olunan açarına üstünlük verir.</summary>
    public TranslationMatch? ResolveNotificationSource(string value)
        => ResolveSource(value, bootstrap.NotificationSourceMatcher);

    /// <summary>Seçilmiş kontekstdə sabit mesajı tanıyır və test ödənişi xəbərdarlığını ayrıca qoruyur.</summary>
    private static TranslationMatch? ResolveSource(string value, TranslationSourceMatcher matcher)
    {
        const string testLabel = "[TEST] ";
        const string testNotice = Payments.PaymentTexts.TestNotice + " ";
        var prefix = value.StartsWith(testNotice, StringComparison.Ordinal) ? testNotice : value.StartsWith(testLabel, StringComparison.Ordinal) ? testLabel : null;
        if (prefix != null)
        {
            var match = matcher.Match(value[prefix.Length..]);
            if (match != null)
            {
                var arguments = match.Arguments.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
                arguments["__prefix"] = prefix == testLabel ? "label" : "notice";
                return match with { Arguments = arguments };
            }
        }
        return matcher.Match(value);
    }

    /// <summary>Yalnız serverin qeydə aldığı test işarəsini qoruyaraq eyni yayımın dilindən şablonu render edir; parametr məzmunu ikinci dəfə şablon kimi işlənmir.</summary>
    public static string Render(IReadOnlyDictionary<string, string> dictionary, string key, string fallback, object? parameters = null)
    {
        var value = Format(dictionary.GetValueOrDefault(key, fallback), parameters);
        if (parameters is not IReadOnlyDictionary<string, object?> arguments || !arguments.TryGetValue("__prefix", out var prefix)) return value;
        return prefix?.ToString() switch
        {
            "label" => "[TEST] " + value,
            "notice" => dictionary.GetValueOrDefault("api.literal.testNotice", Payments.PaymentTexts.TestNotice) + " " + value,
            _ => value
        };
    }

    /// <summary>Yalnız adlı dəyişənləri mətn kimi əvəz edir; format ifadəsi, reflection kodu və HTML icra etmir.</summary>
    public static string Format(string template, object? parameters)
    {
        if (parameters == null) return template;
        var values = parameters is IReadOnlyDictionary<string, object?> dictionary ? dictionary : parameters.GetType()
            .GetProperties(BindingFlags.Instance | BindingFlags.Public).Where(property => property.GetIndexParameters().Length == 0)
            .ToDictionary(property => property.Name, property => property.GetValue(parameters), StringComparer.Ordinal);
        return System.Text.RegularExpressions.Regex.Replace(template, @"\{\{?\s*([a-zA-Z_][a-zA-Z0-9_.]*)\s*\}\}?", match =>
        {
            var parameter = values.FirstOrDefault(pair => string.Equals(pair.Key, match.Groups[1].Value, StringComparison.OrdinalIgnoreCase));
            return parameter.Key == null ? match.Value : Convert.ToString(parameter.Value, CultureInfo.CurrentUICulture) ?? string.Empty;
        }, System.Text.RegularExpressions.RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50));
    }
}
