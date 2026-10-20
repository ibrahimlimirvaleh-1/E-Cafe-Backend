using ECafe.Application.DTOs.ContentLocalization;
using ECafe.Application.Services.ContentLocalization.Abstract;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ApiRoutes = ECafe.Application.Routes.Routes;

namespace ECafe.Api.Controllers;

[ApiController]
[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ContentTranslationsController(IContentTranslationService service) : ControllerBase
{
    /// <summary>Aktiv dəstəklənən dilləri bazadan qaytarır; deaktiv dillər seçim siyahısına çıxmır.</summary>
    [AllowAnonymous]
    [HttpGet(ApiRoutes.Localization.Languages)]
    [ProducesResponseType(typeof(IReadOnlyList<ContentLanguageResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLanguages(CancellationToken cancellationToken)
        => Ok(await service.GetLanguagesAsync(cancellationToken));

    /// <summary>Məzmunun bütün aktiv dillər üzrə xam tərcümələrini və redaktə reviziyalarını göstərir.</summary>
    /// <remarks>Növlər: restaurants, restaurant-groups, categories, items, tables, inventory-items, units. Restoran məzmununu yalnız öz meneceri/sahibkarı; qrup və vahidləri yalnız platforma admini dəyişir.</remarks>
    [HttpGet(ApiRoutes.ContentLocalization.Translations)]
    [ProducesResponseType(typeof(ContentTranslationsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(string entityType, int id, CancellationToken cancellationToken)
        => Ok(await service.GetAsync(entityType, id, cancellationToken));

    /// <summary>EN/RU tərcüməsini yaradır və ya reviziya yoxlaması ilə yeniləyir; AZ üçün əsas create/update axını istifadə edilir.</summary>
    /// <remarks>ExpectedRevision məcburidir: yeni dil sətri üçün 0, mövcud üçün GET-dən gələn reviziya. Boş sahə əsas AZ mətninə fallback deməkdir. Köhnə reviziya 409 qaytarır.</remarks>
    [HttpPut(ApiRoutes.ContentLocalization.Translation)]
    [ProducesResponseType(typeof(ContentTranslationsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Save(string entityType, int id, string languageCode,
        [FromBody] SaveContentTranslationRequest request, CancellationToken cancellationToken)
        => Ok(await service.SaveAsync(entityType, id, languageCode, request, cancellationToken));

    /// <summary>Yalnız seçilmiş EN/RU tərcüməsini silir; entity və əlaqəli biznes məlumatları saxlanılır, oxuma əsas mətnə qayıdır.</summary>
    [HttpDelete(ApiRoutes.ContentLocalization.Translation)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(string entityType, int id, string languageCode,
        [FromQuery] long? expectedRevision, CancellationToken cancellationToken)
    {
        await service.DeleteAsync(entityType, id, languageCode, expectedRevision, cancellationToken);
        return NoContent();
    }
}
