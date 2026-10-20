using ECafe.Application.Repositories.Restaurant;
using ECafe.Domain.Entities;
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
        Type[] types = [typeof(CategoryTranslation), typeof(ItemTranslation), typeof(TableTranslation),
            typeof(InventoryItemTranslation), typeof(UnitTranslation)];
        Assert.Equal(types.OrderBy(type => type.Name), db.Model.GetEntityTypes()
            .Where(entity => typeof(EntityTranslation).IsAssignableFrom(entity.ClrType))
            .Select(entity => entity.ClrType).OrderBy(type => type.Name));
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
    public async Task RestaurantAndGroupPersistCanonicalDataWithoutTranslationEntitiesOrNavigations()
    {
        await using var db = Database();
        await db.Database.EnsureCreatedAsync();
        var group = new RestaurantGroup { Id = 1, Name = "Brend", LegalName = "Legal Name", IsActive = true };
        var restaurant = new Restaurant { Id = 1, Name = "Brend Sahil", BranchName = "Sahil", Location = "Baki",
            Phone = "+994501111111", RestaurantGroup = group, RestaurantGroupId = 1, IsActive = true };
        db.Restaurants.Add(restaurant);
        await db.SaveChangesAsync();
        restaurant.BranchName = "Yeni Sahil";
        group.Name = "Yeni brend";
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var persisted = await db.Restaurants.Include(row => row.RestaurantGroup).SingleAsync(row => row.Id == 1);
        Assert.Equal("Brend Sahil", persisted.Name);
        Assert.Equal("Yeni Sahil", persisted.BranchName);
        Assert.Equal("Baki", persisted.Location);
        Assert.Equal("Yeni brend", persisted.RestaurantGroup!.Name);
        Assert.Equal("Legal Name", persisted.RestaurantGroup.LegalName);
        Assert.Null(db.Model.FindEntityType(typeof(Restaurant))!.FindNavigation("Translations"));
        Assert.Null(db.Model.FindEntityType(typeof(RestaurantGroup))!.FindNavigation("Translations"));
        Assert.Null(typeof(Restaurant).GetProperty("Translations"));
        Assert.Null(typeof(RestaurantGroup).GetProperty("Translations"));
        Assert.Null(typeof(Restaurant).Assembly.GetType("ECafe.Domain.Entities.RestaurantTranslation"));
        Assert.Null(typeof(RestaurantGroup).Assembly.GetType("ECafe.Domain.Entities.RestaurantGroupTranslation"));
        Assert.DoesNotContain(db.Model.GetEntityTypes(), entity => entity.GetTableName() is
            "restaurant_translations" or "restaurant_group_translations");
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
        Assert.DoesNotContain("restaurant_translations", sql);
        Assert.DoesNotContain("restaurant_group_translations", sql);
        Assert.Contains("LIMIT", sql);
        Assert.Contains("is_available", sql);
        Assert.Contains("IsDeleted", sql);
    }

    /// <summary>Şəbəkəyə çıxmadan EF modelini və kompozit əlaqələrin saxlanmasını ayrıca yaddaş bazasında yoxlayır.</summary>
    private static ECafeDbContext Database() => new(new DbContextOptionsBuilder<ECafeDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

}
