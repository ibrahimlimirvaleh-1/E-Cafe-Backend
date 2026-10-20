using ECafe.Application.DTOs.Category;
using ECafe.Application.DTOs.Restaurant;
using ECafe.Application.DTOs.Restaurant.Public;
using ECafe.Application.DTOs.Table;
using ECafe.Application.Repositories.ContentLocalization;
using ECafe.Domain.Enums;
using ItemDto = ECafe.Application.DTOs.Item.ItemDto;

namespace ECafe.Application.Mappings.ContentLocalization;

internal static class CatalogResponseProfiles
{
    public static IReadOnlyList<IContentResponseProfile> All { get; } =
    [
        new ContentResponseProfile<GetAllCategoryResponse>(
            new ContentResponseField<GetAllCategoryResponse>(ContentEntityType.Category, (r, _) => r.Id, r => r.Name, v => v.Name)),
        new ContentResponseProfile<CategoryDto>(
            new ContentResponseField<CategoryDto>(ContentEntityType.Category, (r, _) => r.Id, r => r.Name, v => v.Name)),
        new ContentResponseProfile<PublicMenuCategoryDto>(
            new ContentResponseField<PublicMenuCategoryDto>(ContentEntityType.Category, (r, _) => r.Id, r => r.Name, v => v.Name)),
        new ContentResponseProfile<ItemDto>(
            new(ContentEntityType.Item, (r, _) => r.Id, r => r.Name, v => v.Name),
            new(ContentEntityType.Item, (r, _) => r.Id, r => r.Description, v => v.Description),
            new(ContentEntityType.Category, (r, _) => r.CategoryId, r => r.CategoryName, v => v.Name)),
        new ContentResponseProfile<ECafe.Application.DTOs.Restaurant.ItemDto>(
            new(ContentEntityType.Item, (r, _) => r.Id, r => r.Name, v => v.Name),
            new(ContentEntityType.Item, (r, _) => r.Id, r => r.Description, v => v.Description),
            new(ContentEntityType.Item, (r, _) => r.Id, r => r.UnavailableReason, v => v.UnavailableReason)),
        new ContentResponseProfile<PublicMenuItemDto>(
            new(ContentEntityType.Item, (r, _) => r.Id, r => r.Name, v => v.Name),
            new(ContentEntityType.Item, (r, _) => r.Id, r => r.Description, v => v.Description)),
        new ContentResponseProfile<TableDto>(
            new ContentResponseField<TableDto>(ContentEntityType.Table, (r, _) => r.Id, r => r.Name, v => v.Name)),
        new ContentResponseProfile<TableResponse>(
            new ContentResponseField<TableResponse>(ContentEntityType.Table, (r, _) => r.Id, r => r.Name, v => v.Name)),
        new ContentResponseProfile<PublicTableDto>(
            new ContentResponseField<PublicTableDto>(ContentEntityType.Table, (r, _) => r.Id, r => r.Name, v => v.Name))
    ];
}
