namespace ECafe.Application.Mappings.ContentLocalization;

public static class ContentResponseProfiles
{
    public static IReadOnlyList<IContentResponseProfile> Default { get; } = Array.AsReadOnly(
        RestaurantResponseProfiles.All.Concat(CatalogResponseProfiles.All)
            .Concat(InventoryResponseProfiles.All).Concat(ServiceResponseProfiles.All).ToArray());
}
