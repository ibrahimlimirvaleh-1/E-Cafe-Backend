using ECafe.Domain.Entities;

namespace ECafe.Application.Repositories.Restaurant;

public static class RestaurantSearchQuery
{
    /// <summary>Əsas və seçilmiş dildəki restoran/filial/menyu mətnlərini SQL-də filtr edir; səhifələmə sonrası axtarış etmir.</summary>
    public static IQueryable<Domain.Entities.Restaurant> Search(IQueryable<Domain.Entities.Restaurant> query,
        string term, int languageId, bool includeMenu)
        => query.Where(restaurant => restaurant.Name.ToLower().Contains(term) || restaurant.Location.ToLower().Contains(term) ||
            restaurant.Phone.ToLower().Contains(term) || (restaurant.BranchName != null && restaurant.BranchName.ToLower().Contains(term)) ||
            restaurant.Translations.Any(text => text.LanguageId == languageId &&
                ((text.Name != null && text.Name.ToLower().Contains(term)) || (text.Location != null && text.Location.ToLower().Contains(term)) ||
                 (text.BranchName != null && text.BranchName.ToLower().Contains(term)))) ||
            (restaurant.RestaurantGroup != null && (restaurant.RestaurantGroup.Name.ToLower().Contains(term) ||
                restaurant.RestaurantGroup.Translations.Any(text => text.LanguageId == languageId && text.Name != null && text.Name.ToLower().Contains(term)))) ||
            (includeMenu && restaurant.Categories.Any(category => category.IsActive && !category.IsDeleted &&
                (category.Name.ToLower().Contains(term) || category.Slug.ToLower().Contains(term) ||
                 category.Translations.Any(text => text.LanguageId == languageId && text.Name != null && text.Name.ToLower().Contains(term)) ||
                 category.Items.Any(item => item.IsActive && item.IsAvailable && !item.IsDeleted &&
                    (item.Name.ToLower().Contains(term) || (item.Description != null && item.Description.ToLower().Contains(term)) ||
                     item.Translations.Any(text => text.LanguageId == languageId &&
                        ((text.Name != null && text.Name.ToLower().Contains(term)) ||
                         (text.Description != null && text.Description.ToLower().Contains(term))))))))));
}
