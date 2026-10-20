using System.Security.Claims;
using AutoMapper;
using ECafe.Application.Common.Exceptions;
using ECafe.Application.DTOs.Category;
using ECafe.Application.DTOs.InventoryItem;
using ECafe.Application.DTOs.Item;
using ECafe.Application.DTOs.Table;
using ECafe.Application.Mappings;
using ECafe.Application.Repositories.Category;
using ECafe.Application.Repositories.Restaurant;
using ECafe.Application.Services.AuditLog.Abstract;
using ECafe.Domain.Entities;
using ECafe.Domain.Policies;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using ECafe.Infrastructure.Context;
using ECafe.Infrastructure.Repositories.Category;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace ECafe.Tests;

public sealed class ContentTranslationCrudTests
{
    [Fact]
    public void ItemOmittedTranslationsPreserveOtherLanguagesAndSynchronizeCanonicalAz()
    {
        var item = new Item { Id = 7, Name = "Cay", Description = "Yeni", BasePrice = 4 };
        var en = new ItemTranslation { ItemId = 7, LanguageId = 2, Name = "Tea" };
        var az = new ItemTranslation { ItemId = 7, LanguageId = 1, Name = "Kohne" };
        item.Translations.Add(en);
        item.Translations.Add(az);
        ApplyItem(item, null);
        Assert.Same(en, item.Translations.Single(row => row.LanguageId == 2));
        Assert.Same(az, item.Translations.Single(row => row.LanguageId == 1));
        Assert.Equal("Cay", az.Name);
        Assert.Equal("Yeni", az.Description);
        Assert.Equal(4, item.BasePrice);
    }

    [Fact]
    public void ItemExplicitEmptyTranslationsClearAdditionalLanguagesOnly()
    {
        var item = new Item { Name = "Cay" };
        ApplyItem(item,
            [new() { LanguageCode = "en", Name = "Tea", Description = "Hot" }, new() { LanguageCode = "ru", Name = "Chai" }]);
        ApplyItem(item, []);
        Assert.Equal("Cay", item.Translations.Single(row => row.LanguageId == 1).Name);
        Assert.All(item.Translations.Where(row => row.LanguageId != 1), row => Assert.Null(row.Name));
    }

    [Fact]
    public void ItemReplacementUpdatesTrackedRowAndCanonicalAzIsSynchronized()
    {
        var item = new Item { Id = 7, Name = "Cay" };
        var en = new ItemTranslation { ItemId = 7, LanguageId = 2, Name = "Old tea" };
        item.Translations.Add(en);
        item.Translations.Add(new ItemTranslation { ItemId = 7, LanguageId = 3, Name = "Chai" });
        ApplyItem(item,
            [new() { LanguageCode = "en", Name = " Tea ", Description = " Hot ", UnavailableReason = " Sold out " }]);
        Assert.Equal(3, item.Translations.Count);
        Assert.Null(item.Translations.Single(row => row.LanguageId == 3).Name);
        Assert.Same(en, item.Translations.Single(row => row.LanguageId == 2));
        Assert.Equal("Tea", en.Name);
        Assert.Equal("Hot", en.Description);
        Assert.Equal("Sold out", en.UnavailableReason);
        Assert.Equal("Cay", item.Translations.Single(row => row.LanguageId == 1).Name);
    }

    [Theory]
    [InlineData("en", "en")]
    [InlineData("xx", "ru")]
    [InlineData("EN", "ru")]
    [InlineData("az", "en")]
    public void InvalidOrDuplicateLanguagesDoNotMutateExistingTranslations(string first, string second)
    {
        var item = new Item { Name = "Cay" };
        var existing = new ItemTranslation { LanguageId = 2, Name = "Tea" };
        item.Translations.Add(existing);
        Assert.Throws<BusinessRuleException>(() => ApplyItem(item,
            [new() { LanguageCode = first, Name = "One" }, new() { LanguageCode = second, Name = "Two" }]));
        Assert.Same(existing, Assert.Single(item.Translations));
        Assert.Equal("Tea", existing.Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("<img src=x onerror=alert(1)>")]
    [InlineData("Bad\0name")]
    public void UnsafeOrEmptyRequiredNamesAreRejected(string value)
    {
        Assert.Throws<BusinessRuleException>(() => ApplyCategory(new Category { Name = "Main" },
            [new() { LanguageCode = "en", Name = value }]));
    }

    [Fact]
    public void CollectionIsBoundedAndNullRowsAreRejected()
    {
        var item = new Item { Name = "Cay" };
        Assert.Throws<BusinessRuleException>(() => ApplyItem(item,
            [new(), new(), new(), new()]));
        Assert.Throws<BusinessRuleException>(() => ApplyItem(item, [null!]));
        Assert.Empty(item.Translations);
    }

    [Fact]
    public void FieldLengthsMatchDatabaseLimits()
    {
        Assert.Throws<BusinessRuleException>(() => ApplyItem(new Item { Name = "Cay" },
            [new() { LanguageCode = "en", Name = new string('a', 151) }]));
        Assert.Throws<BusinessRuleException>(() => ApplyItem(new Item { Name = "Cay" },
            [new() { LanguageCode = "en", Name = "Tea", Description = new string('a', 501) }]));
        Assert.Throws<BusinessRuleException>(() => ApplyItem(new Item { Name = "Cay" },
            [new() { LanguageCode = "en", Name = "Tea", UnavailableReason = new string('a', 501) }]));
        Assert.Throws<BusinessRuleException>(() => ApplyCategory(new Category { Name = "Main" },
            [new() { LanguageCode = "en", Name = new string('a', 101) }]));
        Assert.Throws<BusinessRuleException>(() => ApplyTable(new Table(),
            [new() { LanguageCode = "en", Name = new string('a', 51) }]));
        Assert.Throws<BusinessRuleException>(() => ApplyInventoryItem(new InventoryItem { Name = "Stock" },
            [new() { LanguageCode = "en", Name = new string('a', 151) }]));
    }

    [Fact]
    public void TableCopyUsesIndependentTranslationRowsAndOptionalCanonicalNameMayBeEmpty()
    {
        var source = new Table { Id = 8, TableNo = 4, Name = "Garden" };
        ApplyTable(source, [new() { LanguageCode = "en", Name = "Patio" }]);
        var target = new Table { TableNo = 5 };
        CopyTable(source, target);
        Assert.Null(target.Translations.Single(row => row.LanguageId == 1).Name);
        var copied = target.Translations.Single(row => row.LanguageId == 2);
        Assert.NotSame(source.Translations.Single(row => row.LanguageId == 2), copied);
        copied.Name = "Changed";
        Assert.Equal("Patio", source.Translations.Single(row => row.LanguageId == 2).Name);
        Assert.Equal(5, target.TableNo);
        Assert.Equal(4, source.TableNo);
        Assert.Throws<BusinessRuleException>(() => ApplyTable(target,
            [new() { LanguageCode = "ru", Name = " " }]));
    }

    [Fact]
    public void CategoryAndInventoryOmittedVersusEmptyFollowSameContract()
    {
        var category = new Category { Name = "Main" };
        var stock = new InventoryItem { Name = "Stock", QuantityOnHand = 17, UnitId = 2 };
        ApplyCategory(category, [new() { LanguageCode = "en", Name = "Main dishes" }]);
        ApplyInventoryItem(stock, [new() { LanguageCode = "en", Name = "Sugar" }]);
        ApplyCategory(category, null);
        ApplyInventoryItem(stock, null);
        Assert.Equal(2, category.Translations.Count);
        Assert.Equal(2, stock.Translations.Count);
        ApplyCategory(category, []);
        ApplyInventoryItem(stock, []);
        Assert.Null(category.Translations.Single(row => row.LanguageId == 2).Name);
        Assert.Null(stock.Translations.Single(row => row.LanguageId == 2).Name);
        Assert.Equal(17, stock.QuantityOnHand);
        Assert.Equal(2, stock.UnitId);
    }

    [Fact]
    public async Task AnotherRestaurantCannotWriteCategoryOrTranslations()
    {
        var restaurants = new Mock<IRestaurantRepository>();
        restaurants.Setup(repository => repository.GetByIdAsync(2)).ReturnsAsync(new Restaurant { Id = 2 });
        var categories = new Mock<ICategoryRepository>(MockBehavior.Strict);
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([
                new Claim(ClaimTypes.Role, ((int)RoleCode.Manager).ToString()), new Claim("restaurantId", "1")], "test"))
        };
        var manager = new CategoryManager(new HttpContextAccessor { HttpContext = context }, Mock.Of<IMapper>(),
            new ConfigurationBuilder().Build(), categories.Object, restaurants.Object, Mock.Of<IAuditLogService>());
        await Assert.ThrowsAsync<ForbiddenException>(() => manager.CreateCategoryAsync(new CreateCategoryRequest
        {
            RestaurantId = 2, Name = "Main", Translations = [new() { LanguageCode = "en", Name = "Main dishes" }]
        }));
        categories.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task TrackedUpdatesPersistWithoutCompositeKeyIdentityConflict()
    {
        await using var db = new ECafeDbContext(new DbContextOptionsBuilder<ECafeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var item = new Item { Id = 7, Name = "Cay", RestaurantId = 1, CategoryId = 1, StatusId = 5001 };
        ApplyItem(item, [new() { LanguageCode = "en", Name = "Tea" }]);
        db.Set<Item>().Add(item);
        await db.SaveChangesAsync();
        ApplyItem(item, [new() { LanguageCode = "en", Name = "New tea" }]);
        await db.SaveChangesAsync();
        Assert.Equal("New tea", (await db.ItemTranslations.IgnoreQueryFilters().SingleAsync(row => row.ItemId == 7 && row.LanguageId == 2)).Name);
        Assert.Equal(2, await db.ItemTranslations.IgnoreQueryFilters().CountAsync());
        ApplyItem(item, []);
        await db.SaveChangesAsync();
        Assert.Null((await db.ItemTranslations.IgnoreQueryFilters().SingleAsync(row => row.LanguageId == 2)).Name);
        Assert.Equal(3, (await db.ItemTranslations.IgnoreQueryFilters().SingleAsync(row => row.LanguageId == 2)).Revision);
    }

    [Fact]
    public async Task CategoryServicePersistsCreateUpdateClearAndHidesTranslationsAfterDelete()
    {
        await using var db = new ECafeDbContext(new DbContextOptionsBuilder<ECafeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var restaurant = new Restaurant { Id = 1, Name = "Test", Location = "Baku", Phone = "000", IsActive = true };
        db.Restaurants.Add(restaurant);
        db.Languages.AddRange(new Language { Id = 1, Code = "az", Name = "AZ", IsActive = true },
            new Language { Id = 2, Code = "en", Name = "EN", IsActive = true });
        await db.SaveChangesAsync();
        var restaurants = new Mock<IRestaurantRepository>();
        restaurants.Setup(repository => repository.GetByIdAsync(1)).ReturnsAsync(restaurant);
        restaurants.Setup(repository => repository.Query(It.IsAny<Expression<Func<Restaurant, bool>>?>()))
            .Returns((Expression<Func<Restaurant, bool>>? predicate) => predicate is null ? db.Restaurants : db.Restaurants.Where(predicate));
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([
                new Claim(ClaimTypes.Role, ((int)RoleCode.Manager).ToString()), new Claim("restaurantId", "1")], "test"))
        };
        var mapping = new MapperConfiguration(configuration => configuration.AddProfile<CategoryProfile>(),
            Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance).CreateMapper();
        var manager = new CategoryManager(new HttpContextAccessor { HttpContext = context }, mapping,
            new ConfigurationBuilder().Build(), new CategoryRepository(db), restaurants.Object, Mock.Of<IAuditLogService>());
        var id = await manager.CreateCategoryAsync(new CreateCategoryRequest
        {
            RestaurantId = 1, Name = "Main", Translations = [new() { LanguageCode = "en", Name = "Main dishes" }]
        });
        Assert.Equal(2, await db.CategoryTranslations.CountAsync());
        db.ChangeTracker.Clear();
        await manager.UpdateCategoryAsync(1, id, new UpdateCategoryRequest { Name = "New main" });
        Assert.Equal("Main dishes", (await db.CategoryTranslations.SingleAsync(row => row.LanguageId == 2)).Name);
        Assert.Equal("New main", (await db.CategoryTranslations.SingleAsync(row => row.LanguageId == 1)).Name);
        await manager.UpdateCategoryAsync(1, id, new UpdateCategoryRequest { Name = "New main", Translations = [] });
        Assert.Null((await db.CategoryTranslations.SingleAsync(row => row.LanguageId == 2)).Name);
        await manager.DeleteCategoryAsync(1, id);
        Assert.Empty(await db.Categories.ToListAsync());
        Assert.Empty(await db.CategoryTranslations.ToListAsync());
        Assert.Equal(2, await db.CategoryTranslations.IgnoreQueryFilters().CountAsync());
    }

    /// <summary>Məhsul sınaqlarında servisin mapping və domen tətbiqi ardıcıllığını qurur.</summary>
    private static void ApplyItem(Item item, IReadOnlyCollection<ItemTranslationRequest>? requests)
        => ContentTranslationCollectionPolicy.Apply(item.Translations,
            ContentTranslationRequestMapper.MapItem(item.Id, requests),
            ContentTranslationRequestMapper.Canonical(item), ContentTranslationMapper.Copy);

    /// <summary>Kateqoriya sınaqlarında null və boş siyahı müqaviləsini domen policy-si ilə icra edir.</summary>
    private static void ApplyCategory(Category category, IReadOnlyCollection<CategoryTranslationRequest>? requests)
        => ContentTranslationCollectionPolicy.Apply(category.Translations,
            ContentTranslationRequestMapper.MapCategory(category.Id, requests),
            ContentTranslationRequestMapper.Canonical(category), ContentTranslationMapper.Copy);

    /// <summary>Masa sınaqlarında tipli mapping nəticəsini domen kolleksiyasına tətbiq edir.</summary>
    private static void ApplyTable(Table table, IReadOnlyCollection<TableTranslationRequest>? requests)
        => ContentTranslationCollectionPolicy.Apply(table.Translations,
            ContentTranslationRequestMapper.MapTable(table.Id, requests),
            ContentTranslationRequestMapper.Canonical(table), ContentTranslationMapper.Copy);

    /// <summary>Anbar sınaqlarında tərcümə tətbiqini stok sahələrindən ayrı saxlayır.</summary>
    private static void ApplyInventoryItem(InventoryItem item, IReadOnlyCollection<InventoryItemTranslationRequest>? requests)
        => ContentTranslationCollectionPolicy.Apply(item.Translations,
            ContentTranslationRequestMapper.MapInventoryItem(item.Id, requests),
            ContentTranslationRequestMapper.Canonical(item), ContentTranslationMapper.Copy);

    /// <summary>Masa surətinin yeni əlaqə sətirlərini və əsas adını domen kolleksiyasına tətbiq edir.</summary>
    private static void CopyTable(Table source, Table target)
        => ContentTranslationCollectionPolicy.Apply(target.Translations,
            ContentTranslationRequestMapper.MapTableCopy(source, target.Id),
            ContentTranslationRequestMapper.Canonical(target), ContentTranslationMapper.Copy);
}
