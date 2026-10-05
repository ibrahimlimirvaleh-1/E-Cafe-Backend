using System.Security.Claims;
using ECafe.Application.Common.Exceptions;
using ECafe.Application.Repositories.Restaurant;
using ECafe.Application.Repositories.User;
using ECafe.Application.Repositories.UserRestaurant;
using ApiRoutes = ECafe.Application.Routes.Routes;
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

public sealed record StaffMobileAccessResponse(int RestaurantId, bool Enabled);

public sealed record CustomerMobileAccessResponse(bool Enabled);

public sealed record StaffMobileReleaseResponse(
    bool Ready, string? DownloadPath, string? Version, int? VersionCode, long? SizeBytes, string? Sha256);

public sealed record GetMobileModuleQuery(int RestaurantId) : IRequest<MobileModuleResponse>;

public sealed record UpdateMobileModuleCommand(int RestaurantId, bool MobilePushEnabled, bool ShowDownloadLink)
    : IRequest<MobileModuleResponse>;

public sealed record GetPublicMobileReleaseQuery(int? RestaurantId) : IRequest<MobileReleaseResponse>;

public sealed record GetStaffMobileAccessQuery(int RestaurantId) : IRequest<StaffMobileAccessResponse>;

public sealed record GetCustomerMobileAccessQuery : IRequest<CustomerMobileAccessResponse>;

public sealed record GetStaffMobileReleaseQuery(int RestaurantId) : IRequest<StaffMobileReleaseResponse>;

public sealed record GetMobilePublicationQuery : IRequest<MobilePublicationResponse>;

public sealed record UpdateMobilePublicationCommand(bool PublicDownloadEnabled) : IRequest<MobilePublicationResponse>;

public sealed class MobileAppQueryHandler(
    IRestaurantRepository restaurants,
    IUserRepository users,
    IUserRestaurantRepository userRestaurants,
    IMobileAppPublicationStore publicationStore,
    IMobileReleaseArtifactService releaseArtifacts,
    IConfiguration configuration,
    IHttpContextAccessor httpContextAccessor)
    : IRequestHandler<GetMobileModuleQuery, MobileModuleResponse>,
      IRequestHandler<UpdateMobileModuleCommand, MobileModuleResponse>,
      IRequestHandler<GetPublicMobileReleaseQuery, MobileReleaseResponse>,
      IRequestHandler<GetStaffMobileAccessQuery, StaffMobileAccessResponse>,
      IRequestHandler<GetCustomerMobileAccessQuery, CustomerMobileAccessResponse>,
      IRequestHandler<GetStaffMobileReleaseQuery, StaffMobileReleaseResponse>,
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

        return new(restaurant.Id, restaurant.MobilePushEnabled && restaurant.ShowMobileDownloadLink,
            restaurant.ShowMobileDownloadLink);
    }

    public async Task<MobileModuleResponse> Handle(UpdateMobileModuleCommand request, CancellationToken cancellationToken)
    {
        EnsureSuperAdmin();
        if (request.MobilePushEnabled && request.ShowDownloadLink &&
            (!configuration.GetValue<bool>("MobileApp:PushDeliveryReady") ||
             !Guid.TryParse(configuration["MobileApp:ExpoProjectId"], out _)))
            throw new BusinessRuleException("Mobile push delivery is not ready.");

        var restaurant = await restaurants.QueryTracked(r => r.Id == request.RestaurantId)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Restaurant not found.");

        restaurant.MobilePushEnabled = request.ShowDownloadLink && request.MobilePushEnabled;
        restaurant.ShowMobileDownloadLink = request.ShowDownloadLink;
        await restaurants.SaveChangesAsync();

        return new(restaurant.Id, restaurant.MobilePushEnabled, restaurant.ShowMobileDownloadLink);
    }

    public Task<MobileReleaseResponse> Handle(GetPublicMobileReleaseQuery request, CancellationToken cancellationToken)
        => Task.FromResult(HiddenRelease);

    public async Task<CustomerMobileAccessResponse> Handle(GetCustomerMobileAccessQuery request, CancellationToken cancellationToken)
    {
        var user = httpContextAccessor.HttpContext?.User;
        var idClaim = user?.FindFirst("userId")?.Value ?? user?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(idClaim, out var userId) || userId <= 0)
            throw new ForbiddenException("An active customer account is required.");

        var isCustomer = await users.Query(x => x.Id == userId && x.IsActive &&
                x.RoleId == (int)RoleCode.Customer)
            .AnyAsync(cancellationToken);
        if (!isCustomer)
            throw new ForbiddenException("An active customer account is required.");

        return new(true);
    }

    public async Task<StaffMobileAccessResponse> Handle(GetStaffMobileAccessQuery request, CancellationToken cancellationToken)
    {
        var user = httpContextAccessor.HttpContext?.User;
        var idClaim = user?.FindFirst("userId")?.Value ?? user?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(idClaim, out var userId))
            throw new ForbiddenException("An active restaurant staff account is required.");

        var roleId = await userRestaurants.GetActiveRoleIdAsync(userId, request.RestaurantId);
        if (roleId != (int)RoleCode.Owner && roleId != (int)RoleCode.Manager &&
            roleId != (int)RoleCode.Waiter && roleId != (int)RoleCode.Kitchen)
            throw new ForbiddenException("An active restaurant staff assignment is required.");

        var activeContractStatusId = StatusIds.Contract(ContractStatus.Active);
        var enabled = await restaurants.Query(r => r.Id == request.RestaurantId && r.IsActive &&
                r.ShowMobileDownloadLink &&
                r.Contracts.Any(c => c.StatusId == activeContractStatusId))
            .AnyAsync(cancellationToken);
        return new(request.RestaurantId, enabled);
    }

    public async Task<StaffMobileReleaseResponse> Handle(GetStaffMobileReleaseQuery request, CancellationToken cancellationToken)
    {
        var access = await Handle(new GetStaffMobileAccessQuery(request.RestaurantId), cancellationToken);
        if (!access.Enabled)
            return new(false, null, null, null, null, null);

        await using var release = await releaseArtifacts.OpenVerifiedAsync(cancellationToken);
        if (release is null)
            return new(false, null, null, null, null, null);

        var path = "/" + ApiRoutes.MobileApp.StaffRestaurantDownload.Replace(
            "{restaurantId:int}", request.RestaurantId.ToString());
        return new(true, path, release.Version, release.VersionCode, release.SizeBytes, release.Sha256);
    }

    public async Task<MobilePublicationResponse> Handle(GetMobilePublicationQuery request, CancellationToken cancellationToken)
    {
        EnsureSuperAdmin();
        await using var release = await releaseArtifacts.OpenVerifiedAsync(cancellationToken);
        return new(false, release is not null);
    }

    public async Task<MobilePublicationResponse> Handle(UpdateMobilePublicationCommand request, CancellationToken cancellationToken)
    {
        EnsureSuperAdmin();
        if (request.PublicDownloadEnabled)
            throw new BusinessRuleException("Public mobile download is disabled. Enable access per restaurant.");

        await publicationStore.SetPublicDownloadEnabledAsync(false, cancellationToken);
        await using var release = await releaseArtifacts.OpenVerifiedAsync(cancellationToken);
        return new(false, release is not null);
    }

    private void EnsureSuperAdmin()
    {
        var role = httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.Role)?.Value;
        if (role != ((int)RoleCode.SuperAdmin).ToString())
            throw new ForbiddenException("Only the platform administrator can manage mobile modules.");
    }
}
