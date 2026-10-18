using ECafe.Application.DTOs.Localization;
using ECafe.Application.Features.Localization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ApiRoutes = ECafe.Application.Routes.Routes;

namespace ECafe.Api.Controllers;

[Authorize(Roles = "1")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class LocalizationAdminController : BaseController
{
    /// <summary>Platforma admininə cari qaralama, reviziya və yayımlanmış versiyanı göstərir.</summary>
    [HttpGet(ApiRoutes.Localization.Draft)]
    public async Task<IActionResult> GetDraft(CancellationToken cancellationToken)
        => Ok(await Mediator.Send(new TranslationDraftQuery(), cancellationToken));
    /// <summary>Məlum açarların üç dildə mətnlərini qaralamaya saxlayır; istifadəçilərə hələ göstərmir.</summary>
    /// <remarks>Bir sorğuda maksimum 250 açar. ExpectedRevision köhnədirsə 409. Dəyişən adları bütün dillərdə və ilkin mətndə eyni qalmalıdır. HTML və kod qəbul edilmir.</remarks>
    [HttpPut(ApiRoutes.Localization.Draft)]
    public async Task<IActionResult> SaveDraft([FromBody] SaveTranslationDraftRequest request, CancellationToken cancellationToken)
        => Ok(await Mediator.Send(new SaveTranslationDraftCommand(request), cancellationToken));
    /// <summary>Qaralamanı atomik yeni versiya kimi yayımlayır və server keşini yeniləyir.</summary>
    [HttpPost(ApiRoutes.Localization.Publish)]
    public async Task<IActionResult> Publish([FromBody] PublishTranslationRequest request, CancellationToken cancellationToken)
        => Ok(await Mediator.Send(new PublishTranslationCommand(request.ExpectedRevision, null), cancellationToken));
    /// <summary>Son 50 yayımın müəllif və tarix metadata-sını göstərir.</summary>
    [HttpGet(ApiRoutes.Localization.Releases)]
    public async Task<IActionResult> GetReleases(CancellationToken cancellationToken)
        => Ok(await Mediator.Send(new TranslationReleasesQuery(), cancellationToken));
    /// <summary>Köhnə yayımlanmış lüğəti yeni versiya kimi bərpa edir; tarixçə silinmir və versiya geriyə getmir.</summary>
    [HttpPost(ApiRoutes.Localization.Rollback)]
    public async Task<IActionResult> Rollback(int releaseId, [FromBody] PublishTranslationRequest request, CancellationToken cancellationToken)
        => Ok(await Mediator.Send(new PublishTranslationCommand(request.ExpectedRevision, releaseId), cancellationToken));
}
