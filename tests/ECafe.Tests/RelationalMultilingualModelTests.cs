using ECafe.Application.Mappings;
using ECafe.Application.Repositories.Restaurant;
using ECafe.Domain.Entities;
using ECafe.Domain.Policies;
using ECafe.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ECafe.Tests;

public sealed class RelationalMultilingualModelTests
{
    [Fact]
    public void EachContentTranslationUsesItsOwnCompositeKeyAndRealForeignKeys()
    {
        using var db = Database();
        Assert.Null(db.Model.FindEntityType(typeof(EntityTranslation)));
        Type[] types = [typeof(RestaurantTranslation), typeof(RestaurantGroupTranslation), typeof(CategoryTranslation),
            typeof(ItemTranslation), typeof(TableTranslation), typeof(InventoryItemTranslation), typeof(UnitTranslation)];
        foreach (var type in types)
        {
            var entity = db.Model.FindEntityType(type)!;
            Assert.Equal(2, entity.FindPrimaryKey()!.Properties.Count);
            Assert.Contains(entity.FindPrimaryKey()!.Properties, property => property.Name == "LanguageId");
            Assert.Equal(2, entity.GetForeignKeys().Count());
            Assert.Contains(entity.GetForeignKeys(), foreignKey => foreignKey.PrincipalEntityType.ClrType == typeof(Language));
            Assert.True(entity.FindProperty("Revision")!.IsConcurrencyToken);
            Assert.NotNull(entity.GetQueryFilter());
        }
    }

    [Fact]
    public void EditableAndPublishedSystemTranslationsAreNormalizedNotJsonColumns()
    {
        using var db = Database();
        Assert.Null(db.Model.FindEntityType(typeof(TranslationCatalog))!.FindProperty("DraftJson"));
        Assert.Null(db.Model.FindEntityType(typeof(TranslationRelease))!.FindProperty("MessagesJson"));
        Assert.NotNull(db.Model.FindEntityType(typeof(KeywordTranslation))!.FindPrimaryKey());
        Assert.Equal(3, db.Model.FindEntityType(typeof(TranslationReleaseEntry))!.FindPrimaryKey()!.Properties.Count);
    }

    [Fact]
    public async Task RestaurantAndGroupTranslationsPreserveCanonicalDataAndOmittedLanguages()
    {
        await using var db = Database();
        await db.Database.EnsureCreatedAsync();
        var group = new RestaurantGroup { Id = 1, Name = "Brend", LegalName = "Legal Name", IsActive = true };
        var restaurant = new Restaurant { Id = 1, Name = "Brend Sahil", BranchName = "Sahil", Location = "Baki",
            Phone = "+994501111111", RestaurantGroup = group, RestaurantGroupId = 1, IsActive = true };
        ApplyGroup(group, [new() { LanguageCode = "en", Name = "Brand" }]);
        ApplyRestaurant(restaurant, [new() { LanguageCode = "en", Name = "Brand Coast", Location = "Baku" }]);
        db.Restaurants.Add(restaurant);
        await db.SaveChangesAsync();
        restaurant.BranchName = "Yeni Sahil";
        ApplyRestaurant(restaurant, null);
        await db.SaveChangesAsync();
        Assert.Equal("Brand Coast", restaurant.Translations.Single(row => row.LanguageId == 2).Name);
        Assert.Equal("Yeni Sahil", restaurant.Translations.Single(row => row.LanguageId == 1).BranchName);
        Assert.Equal("Brend Sahil", restaurant.Name);
        Assert.Equal("Legal Name", group.LegalName);
        ApplyRestaurant(restaurant, []);
        await db.SaveChangesAsync();
        Assert.Null(restaurant.Translations.Single(row => row.LanguageId == 2).Name);
        Assert.Equal(2, await db.RestaurantTranslations.CountAsync());
    }

    [Fact]
    public void TranslatedSearchRemainsServerSideAndKeepsMenuVisibilityPredicates()
    {
        using var db = new ECafeDbContext(new DbContextOptionsBuilder<ECafeDbContext>()
            .UseNpgsql("Host=localhost;Database=not_connected;Username=test;Password=test").Options);
        var sql = RestaurantSearchQuery.Search(db.Restaurants.AsNoTracking(), "tea", 2, true)
            .OrderBy(row => row.Name).ThenBy(row => row.Id).Skip(0).Take(20).ToQueryString();
        Assert.Contains("item_translations", sql);
        Assert.Contains("category_translations", sql);
        Assert.Contains("restaurant_translations", sql);
        Assert.Contains("LIMIT", sql);
        Assert.Contains("is_available", sql);
        Assert.Contains("IsDeleted", sql);
    }

    /// <summary>Şəbəkəyə çıxmadan EF modelini və kompozit əlaqələrin saxlanmasını ayrıca yaddaş bazasında yoxlayır.</summary>
    private static ECafeDbContext Database() => new(new DbContextOptionsBuilder<ECafeDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    /// <summary>Restoran sınağında mapping nəticəsini AZ mənbə ilə birlikdə domen policy-sinə ötürür.</summary>
    private static void ApplyRestaurant(Restaurant restaurant, IReadOnlyList<ECafe.Application.DTOs.Localization.RestaurantTranslationInput>? requests)
        => ContentTranslationCollectionPolicy.Apply(restaurant.Translations,
            RestaurantTranslationMapper.MapRestaurant(restaurant.Id, requests),
            RestaurantTranslationMapper.Canonical(restaurant), ContentTranslationMapper.Copy);

    /// <summary>Qrup sınağında hüquqi məlumatları dəyişmədən tərcümə kolleksiyasını yeniləyir.</summary>
    private static void ApplyGroup(RestaurantGroup group, IReadOnlyList<ECafe.Application.DTOs.Localization.RestaurantGroupTranslationInput>? requests)
        => ContentTranslationCollectionPolicy.Apply(group.Translations,
            RestaurantTranslationMapper.MapGroup(group.Id, requests),
            RestaurantTranslationMapper.Canonical(group), ContentTranslationMapper.Copy);
}
