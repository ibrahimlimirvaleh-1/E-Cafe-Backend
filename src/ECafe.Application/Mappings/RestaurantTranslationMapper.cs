using ECafe.Application.DTOs.Localization;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using ECafe.Domain.Policies;

namespace ECafe.Application.Mappings;

public static class RestaurantTranslationMapper
{
    /// <summary>Restoran sorğularını domenin normallaşdırdığı sahələrdən yeni tərcümə sətirlərinə çevirir.</summary>
    public static List<RestaurantTranslation>? MapRestaurant(int restaurantId, IReadOnlyList<RestaurantTranslationInput>? translations)
        => Map(translations, input =>
        {
            var languageId = ContentTranslationPolicy.EditableLanguageId(input.LanguageCode);
            var fields = ContentTranslationPolicy.Normalize(ContentEntityType.Restaurant,
                new(input.Name, Location: input.Location, BranchName: input.BranchName), trimBeforeLengthCheck: true);
            return new RestaurantTranslation
            {
                RestaurantId = restaurantId, LanguageId = languageId,
                Name = fields.Name, BranchName = fields.BranchName, Location = fields.Location
            };
        });

    /// <summary>Qrup sorğusunu yeni tərcümə sətirlərinə çevirir; hüquqi məlumatları və izlənən kolleksiyanı dəyişmir.</summary>
    public static List<RestaurantGroupTranslation>? MapGroup(int groupId, IReadOnlyList<RestaurantGroupTranslationInput>? translations)
        => Map(translations, input =>
        {
            var languageId = ContentTranslationPolicy.EditableLanguageId(input.LanguageCode);
            var fields = ContentTranslationPolicy.Normalize(ContentEntityType.RestaurantGroup, new(input.Name),
                requireName: true, trimBeforeLengthCheck: true);
            return new RestaurantGroupTranslation
            {
                RestaurantGroupId = groupId, LanguageId = languageId, Name = fields.Name
            };
        });

    /// <summary>Restoranın əsas sahələrini AZ sətrinin mənbə modeli kimi hazırlayır.</summary>
    public static RestaurantTranslation Canonical(Restaurant restaurant) => new()
    {
        RestaurantId = restaurant.Id, LanguageId = TranslationPolicy.DefaultLanguageId, Name = restaurant.Name, BranchName = restaurant.BranchName, Location = restaurant.Location
    };

    /// <summary>Qrupun əsas adını AZ sətrinin mənbə modeli kimi hazırlayır; hüquqi adı daxil etmir.</summary>
    public static RestaurantGroupTranslation Canonical(RestaurantGroup group)
        => new() { RestaurantGroupId = group.Id, LanguageId = TranslationPolicy.DefaultLanguageId, Name = group.Name };

    /// <summary>Sətir həddini domenə yoxladır və sorğunu izlənən obyektə toxunmadan tam materiallaşdırır.</summary>
    private static List<TEntity>? Map<TInput, TEntity>(IReadOnlyList<TInput>? inputs, Func<TInput, TEntity> map)
    {
        if (inputs is null) return null;
        ContentTranslationPolicy.RequireAdditionalLanguageCount(inputs.Count);
        return inputs.Select(input => input is null
            ? throw new BusinessRuleException(ErrorCode.LocalizationInvalidEntry)
            : map(input)).ToList();
    }
}
