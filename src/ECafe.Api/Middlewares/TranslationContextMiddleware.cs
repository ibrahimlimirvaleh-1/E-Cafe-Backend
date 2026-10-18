using System.Globalization;
using ECafe.Application.Services.Localization.Abstract;
using ECafe.Application.Services.Localization.Concrete;

namespace ECafe.Api.Middlewares;

public sealed class TranslationContextMiddleware(RequestDelegate next)
{
    /// <summary>Aktiv sorğunun yayımlanmış lüğətini bir dəfə seçir; sonrakı xəta və cavab mətnləri eyni dil/versiyadan istifadə edir.</summary>
    public async Task InvokeAsync(HttpContext context, ITranslationService translations)
    {
        var snapshot = await translations.GetPublishedAsync(context.RequestAborted);
        context.Items[TextLocalizer.ContextKey] = snapshot.ForLanguage(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
        context.Response.Headers.Append("Vary", "Accept-Language");
        await next(context);
    }
}
