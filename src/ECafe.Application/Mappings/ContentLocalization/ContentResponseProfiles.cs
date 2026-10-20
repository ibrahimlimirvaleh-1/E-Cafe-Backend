namespace ECafe.Application.Mappings.ContentLocalization;

public static class ContentResponseProfiles
{
    public static IReadOnlyList<IContentResponseProfile> Default { get; } = Array.AsReadOnly(
        CatalogResponseProfiles.All.Concat(InventoryResponseProfiles.All).Concat(ServiceResponseProfiles.All).ToArray());
}
