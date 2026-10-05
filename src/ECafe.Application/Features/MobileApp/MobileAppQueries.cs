using System.Security.Claims;
using ECafe.Application.Common.Exceptions;
using ECafe.Application.Repositories.Restaurant;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ECafe.Application.Features.MobileApp;

public sealed record MobileModuleResponse(int RestaurantId, bool MobilePushEnabled, bool ShowDownloadLink);

public sealed record MobileReleaseResponse(
    bool IsVisible,
    bool Ready,
    string? ApkUrl,
    string? Version,
    int? VersionCode,
    long? SizeBytes,
    string? Sha256);

public sealed record UpdateMobileModuleRequest(bool MobilePushEnabled, bool ShowDownloadLink);

public sealed record MobilePublicationResponse(bool PublicDownloadEnabled, bool ReleaseReady);

public sealed record UpdateMobilePublicationRequest(bool PublicDownloadEnabled);

public sealed record GetMobileModuleQuery(int RestaurantId) : IRequest<MobileModuleResponse>;

public sealed record UpdateMobileModuleCommand(int RestaurantId, bool MobilePushEnabled, bool ShowDownloadLink)
    : IRequest<MobileModuleResponse>;

public sealed record GetPublicMobileReleaseQuery(int? RestaurantId) : IRequest<MobileReleaseResponse>;

public sealed record GetMobilePublicationQuery : IRequest<MobilePublicationResponse>;

public sealed record UpdateMobilePublicationCommand(bool PublicDownloadEnabled) : IRequest<MobilePublicationResponse>;

public sealed class MobileAppQueryHandler(
    IRestaurantRepository restaurants,
    IMobileAppPublicationStore publicationStore,
    IConfiguration configuration,
    IHttpContextAccessor httpContextAccessor)
    : IRequestHandler<GetMobileModuleQuery, MobileModuleResponse>,
      IRequestHandler<UpdateMobileModuleCommand, MobileModuleResponse>,
      IRequestHandler<GetPublicMobileReleaseQuery, MobileReleaseResponse>,
      IRequestHandler<GetMobilePublicationQuery, MobilePublicationResponse>,
      IRequestHandler<UpdateMobilePublicationCommand, MobilePublicationResponse>
{
    private static readonly MobileReleaseResponse HiddenRelease = new(false, false, null, null, null, null, null);

    public async Task<MobileModuleResponse> Handle(GetMobileModuleQuery request, CancellationToken cancellationToken)
    {
        EnsureSuperAdmin();
        var restaurant = await restaurants.Query(r => r.Id == request.RestaurantId)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Restaurant not found.");

        return new(restaurant.Id, restaurant.MobilePushEnabled, restaurant.ShowMobileDownloadLink);
    }

    public async Task<MobileModuleResponse> Handle(UpdateMobileModuleCommand request, CancellationToken cancellationToken)
    {
        EnsureSuperAdmin();
        if (request.MobilePushEnabled &&
            (!configuration.GetValue<bool>("MobileApp:PushDeliveryReady") ||
             !Guid.TryParse(configuration["MobileApp:ExpoProjectId"], out _)))
            throw new BusinessRuleException("Mobile push delivery is not ready.");

        var restaurant = await restaurants.QueryTracked(r => r.Id == request.RestaurantId)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Restaurant not found.");

        restaurant.MobilePushEnabled = request.MobilePushEnabled;
        restaurant.ShowMobileDownloadLink = request.ShowDownloadLink;
        await restaurants.SaveChangesAsync();

        return new(restaurant.Id, restaurant.MobilePushEnabled, restaurant.ShowMobileDownloadLink);
    }

    public async Task<MobileReleaseResponse> Handle(GetPublicMobileReleaseQuery request, CancellationToken cancellationToken)
    {
        var release = ReadReadyRelease();
        if (release is null)
            return HiddenRelease;

        var activeContractStatusId = StatusIds.Contract(ContractStatus.Active);
        var eligibleRestaurants = restaurants.Query(r =>
                r.IsActive &&
                r.ShowMobileDownloadLink &&
                r.Contracts.Any(c => c.StatusId == activeContractStatusId));

        if (request.RestaurantId is null)
        {
            if (!await publicationStore.IsPublicDownloadEnabledAsync(cancellationToken))
                return HiddenRelease;

            return await eligibleRestaurants.AnyAsync(cancellationToken) ? release : HiddenRelease;
        }

        var isVisible = await eligibleRestaurants.Where(r => r.Id == request.RestaurantId.Value)
            .AnyAsync(cancellationToken);

        return isVisible ? release : HiddenRelease;
    }

    public async Task<MobilePublicationResponse> Handle(GetMobilePublicationQuery request, CancellationToken cancellationToken)
    {
        EnsureSuperAdmin();
        return new(await publicationStore.IsPublicDownloadEnabledAsync(cancellationToken), ReadReadyRelease() is not null);
    }

    public async Task<MobilePublicationResponse> Handle(UpdateMobilePublicationCommand request, CancellationToken cancellationToken)
    {
        EnsureSuperAdmin();
        if (request.PublicDownloadEnabled && ReadReadyRelease() is null)
            throw new BusinessRuleException("A verified mobile release is required before public download can be enabled.");

        await publicationStore.SetPublicDownloadEnabledAsync(request.PublicDownloadEnabled, cancellationToken);
        return new(request.PublicDownloadEnabled, ReadReadyRelease() is not null);
    }

    private MobileReleaseResponse? ReadReadyRelease()
    {
        var section = configuration.GetSection("MobileApp:Release");
        var url = section["ApkUrl"];
        var version = section["Version"];
        var sha256 = section["Sha256"];
        var versionCode = section.GetValue<int>("VersionCode");
        var sizeBytes = section.GetValue<long>("SizeBytes");
        if (!section.GetValue<bool>("Ready") ||
            !Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            !string.IsNullOrEmpty(uri.Fragment) ||
            string.IsNullOrWhiteSpace(version) ||
            versionCode <= 0 ||
            sizeBytes <= 0 ||
            sha256 is null || sha256.Length != 64 || !sha256.All(Uri.IsHexDigit))
            return null;

        return new(true, true, uri.AbsoluteUri, version, versionCode, sizeBytes, sha256.ToLowerInvariant());
    }

    private void EnsureSuperAdmin()
    {
        var role = httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.Role)?.Value;
        if (role != ((int)RoleCode.SuperAdmin).ToString())
            throw new ForbiddenException("Only the platform administrator can manage mobile modules.");
    }
}
