using ECafe.Application.DTOs.Localization;
using ECafe.Application.Features.Localization;
using ECafe.Domain.Policies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using ApiRoutes = ECafe.Application.Routes.Routes;

namespace ECafe.Api.Controllers;

public sealed class LocalizationController : BaseController
{
    /// <summary>AZ, EN və RU dillərində yalnız yayımlanmış, şəxsi məlumat olmayan lüğəti verir.</summary>
    /// <remarks>Qaralama görünmür. Version qeyri-şəffaf identifikatordur. If-None-Match uyğun gəldikdə 304 qaytarılır; browser lüğəti saxlayıb versiyanı yenidən yoxlaya bilər.</remarks>
    [AllowAnonymous]
    [HttpGet(ApiRoutes.Localization.Dictionary)]
    [ProducesResponseType(typeof(TranslationDictionaryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    public async Task<IActionResult> GetDictionary(string language, CancellationToken cancellationToken)
    {
        TranslationPolicy.ValidateLanguage(language);
        var snapshot = await Mediator.Send(new TranslationDictionaryQuery(), cancellationToken);
        var etag = $"\"{snapshot.Version}-{language}\"";
        Response.Headers.ETag = etag;
        Response.Headers.CacheControl = "public, max-age=60, stale-while-revalidate=3600";
        Response.Headers.ContentLanguage = language;
        if (EntityTagHeaderValue.TryParseList(Request.Headers.IfNoneMatch.Where(value => value != null).Select(value => value!).ToArray(), out var tags) &&
            tags.Any(tag => tag.Tag.Value == "*" || tag.Tag.Value == etag)) return StatusCode(StatusCodes.Status304NotModified);
        return Ok(snapshot.ForLanguage(language));
    }

    /// <summary>Aktiv istifadəçinin hesabına saxlanmış dil seçimini qaytarır; standart Azərbaycan dilidir.</summary>
    [Authorize]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [HttpGet(ApiRoutes.Localization.Preference)]
    public async Task<IActionResult> GetPreference(CancellationToken cancellationToken)
        => Ok(await Mediator.Send(new LanguagePreferenceQuery(), cancellationToken));

    /// <summary>Yalnız cari istifadəçinin AZ/EN/RU dil seçimini saxlayır; hesab rolunu və sessiyanı dəyişmir.</summary>
    [Authorize]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [HttpPut(ApiRoutes.Localization.Preference)]
    public async Task<IActionResult> SavePreference([FromBody] LanguagePreferenceRequest request, CancellationToken cancellationToken)
        => Ok(await Mediator.Send(new SaveLanguagePreferenceCommand(request.Language), cancellationToken));
}
