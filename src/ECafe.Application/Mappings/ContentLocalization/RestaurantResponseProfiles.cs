using ECafe.Application.DTOs.Restaurant;
using ECafe.Application.DTOs.Restaurant.Public;
using ECafe.Application.DTOs.RestaurantGroup;
using ECafe.Application.Repositories.ContentLocalization;
using ECafe.Domain.Enums;

namespace ECafe.Application.Mappings.ContentLocalization;

internal static class RestaurantResponseProfiles
{
    public static IReadOnlyList<IContentResponseProfile> All { get; } =
    [
        new ContentResponseProfile<GetAllRestaurantsResponse>(
            new(ContentEntityType.Restaurant, (r, _) => r.Id, r => r.Name, v => v.Name),
            new(ContentEntityType.Restaurant, (r, _) => r.Id, r => r.Location, v => v.Location),
            new(ContentEntityType.Restaurant, (r, _) => r.Id, r => r.BranchName, v => v.BranchName),
            new(ContentEntityType.RestaurantGroup, (r, _) => r.RestaurantGroupId, r => r.RestaurantGroupName, v => v.Name)),
        new ContentResponseProfile<PublicRestaurantListItemDto>(
            new(ContentEntityType.Restaurant, (r, _) => r.Id, r => r.Name, v => v.Name),
            new(ContentEntityType.Restaurant, (r, _) => r.Id, r => r.Location, v => v.Location),
            new(ContentEntityType.Restaurant, (r, _) => r.Id, r => r.BranchName, v => v.BranchName),
            new(ContentEntityType.RestaurantGroup, (r, _) => r.RestaurantGroupId, r => r.RestaurantGroupName, v => v.Name)),
        new ContentResponseProfile<PublicRestaurantDetailDto>(
            new(ContentEntityType.Restaurant, (r, _) => r.Id, r => r.Name, v => v.Name),
            new(ContentEntityType.Restaurant, (r, _) => r.Id, r => r.Location, v => v.Location),
            new(ContentEntityType.Restaurant, (r, _) => r.Id, r => r.BranchName, v => v.BranchName),
            new(ContentEntityType.RestaurantGroup, (r, _) => r.RestaurantGroupId, r => r.RestaurantGroupName, v => v.Name)),
        new ContentResponseProfile<RestaurantDetailDto>(
            new(ContentEntityType.Restaurant, (_, c) => c.RestaurantId, r => r.Name, v => v.Name),
            new(ContentEntityType.Restaurant, (_, c) => c.RestaurantId, r => r.Location, v => v.Location),
            new(ContentEntityType.Restaurant, (_, c) => c.RestaurantId, r => r.BranchName, v => v.BranchName),
            new(ContentEntityType.RestaurantGroup, (r, _) => r.RestaurantGroupId, r => r.RestaurantGroupName, v => v.Name)),
        new ContentResponseProfile<RestaurantGroupResponse>(
            new ContentResponseField<RestaurantGroupResponse>(ContentEntityType.RestaurantGroup, (r, _) => r.Id, r => r.Name, v => v.Name))
    ];
}
