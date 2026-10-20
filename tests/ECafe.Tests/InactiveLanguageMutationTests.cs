using System.Linq.Expressions;
using System.Security.Claims;
using AutoMapper;
using ECafe.Application.DTOs.Category;
using ECafe.Application.Mappings;
using ECafe.Application.Services.AuditLog.Abstract;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using ECafe.Infrastructure.Context;
using ECafe.Infrastructure.Repositories.Category;
using ECafe.Infrastructure.Repositories.InventoryItem;
using ECafe.Infrastructure.Repositories.Item;
using ECafe.Infrastructure.Repositories.Restaurant;
using ECafe.Infrastructure.Repositories.Table;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ECafe.Tests;

public sealed class InactiveLanguageMutationTests
{
    [Theory]
    [InlineData("replace", "New English", 8L)]
    [InlineData("clear", null, 8L)]
    [InlineData("omit", "Old English", 7L)]
    public async Task CategorySourceMutationPreservesInactiveIdentityAndRevisionAfterReactivation(
        string operation, string? expectedName, long expectedRevision)
    {
        var options = Options();
        await SeedAsync(options);

        await using (var mutation = new ECafeDbContext(options))
        {
            Assert.Empty(mutation.ChangeTracker.Entries());
            var request = new UpdateCategoryRequest { Name = "Updated category", SortOrder = 1 };
            if (operation == "replace")
                request.Translations = [new() { LanguageCode = "en", Name = "New English" }];
            else if (operation == "clear")
                request.Translations = [];

            await Manager(mutation).UpdateCategoryAsync(1, 7, request);
            var tracked = mutation.ChangeTracker.Entries<CategoryTranslation>()
                .Single(entry => entry.Entity.LanguageId == 2);
            Assert.Equal(7, tracked.Entity.CategoryId);
            Assert.Equal(expectedRevision, tracked.Entity.Revision);
            Assert.Equal(expectedName, tracked.Entity.Name);
            Assert.Single(await mutation.CategoryTranslations.AsNoTracking().Where(row => row.CategoryId == 7).ToListAsync());
        }

        await using (var activation = new ECafeDbContext(options))
        {
            var retained = await activation.CategoryTranslations.IgnoreQueryFilters().AsNoTracking()
                .Where(row => row.CategoryId == 7).ToListAsync();
            Assert.Equal(2, retained.Count);
            Assert.Equal(expectedName, retained.Single(row => row.LanguageId == 2).Name);
            Assert.Equal("Other English", (await activation.CategoryTranslations.IgnoreQueryFilters()
                .AsNoTracking().SingleAsync(row => row.CategoryId == 8 && row.LanguageId == 2)).Name);
            (await activation.Languages.SingleAsync(row => row.Id == 2)).IsActive = true;
            await activation.SaveChangesAsync();
        }

        await using var read = new ECafeDbContext(options);
        var visible = await read.CategoryTranslations.AsNoTracking().Where(row => row.CategoryId == 7).ToListAsync();
        Assert.Equal(2, visible.Count);
        Assert.Equal("Updated category", visible.Single(row => row.LanguageId == 1).Name);
        var english = visible.Single(row => row.LanguageId == 2);
        Assert.Equal(expectedName, english.Name);
        Assert.Equal(expectedRevision, english.Revision);
    }

    [Theory]
    [InlineData("foreign", ErrorCode.CategoryNotFound)]
    [InlineData("deleted-category", ErrorCode.CategoryNotFound)]
    [InlineData("deleted-restaurant", ErrorCode.RestaurantNotFound)]
    public async Task CategoryMutationKeepsOwnerScopeAndSoftDeleteFilters(string scenario, ErrorCode expectedCode)
    {
        var options = Options();
        await SeedAsync(options);
        await using (var setup = new ECafeDbContext(options))
        {
            if (scenario == "deleted-category")
                (await setup.Categories.SingleAsync(row => row.Id == 7)).IsDeleted = true;
            else if (scenario == "deleted-restaurant")
                (await setup.Restaurants.SingleAsync(row => row.Id == 1)).IsDeleted = true;
            await setup.SaveChangesAsync();
        }

        await using var mutation = new ECafeDbContext(options);
        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() => Manager(mutation)
            .UpdateCategoryAsync(1, scenario == "foreign" ? 8 : 7,
                new UpdateCategoryRequest { Name = "Forbidden change", Translations = [] }));
        Assert.Equal(expectedCode, exception.Code);
        Assert.Empty(mutation.ChangeTracker.Entries<CategoryTranslation>());

        await using var read = new ECafeDbContext(options);
        Assert.Equal("Old English", (await read.CategoryTranslations.IgnoreQueryFilters().AsNoTracking()
            .SingleAsync(row => row.CategoryId == 7 && row.LanguageId == 2)).Name);
    }

    [Theory]
    [InlineData("restaurant")]
    [InlineData("table")]
    [InlineData("item")]
    [InlineData("category")]
    [InlineData("inventory")]
    public async Task EveryRepositoryLoadsOnlyItsOwnersInactiveTranslationsEvenAfterFilteredInclude(string type)
    {
        var options = Options();
        await SeedAsync(options);
        await using var db = new ECafeDbContext(options);
        switch (type)
        {
            case "restaurant":
                await VerifyLoadAsync(db.Restaurants.Where(row => row.Id == 1),
                    row => row.Translations, new RestaurantRepository(db).LoadTranslationsAsync);
                break;
            case "table":
                await VerifyLoadAsync(db.Tables.Where(row => row.Id == 7),
                    row => row.Translations, new TableRepository(db).LoadTranslationsAsync);
                break;
            case "item":
                await VerifyLoadAsync(db.Items.Where(row => row.Id == 7),
                    row => row.Translations, new ItemRepository(db).LoadTranslationsAsync);
                break;
            case "category":
                await VerifyLoadAsync(db.Categories.Where(row => row.Id == 7),
                    row => row.Translations, new CategoryRepository(db).LoadTranslationsAsync);
                break;
            case "inventory":
                await VerifyLoadAsync(db.InventoryItems.Where(row => row.Id == 7),
                    row => row.Translations, new InventoryItemRepository(db).LoadTranslationsAsync);
                break;
        }
        Assert.Equal(2, db.ChangeTracker.Entries<EntityTranslation>().Count());
    }

    /// <summary>Filtrlənmiş Include-dan sonra da yalnız sahibin bütün dil sətirlərinin izlənərək yükləndiyini yoxlayır.</summary>
    private static async Task VerifyLoadAsync<TOwner, TTranslation>(IQueryable<TOwner> query,
        Expression<Func<TOwner, ICollection<TTranslation>>> navigation,
        Func<TOwner, CancellationToken, Task> load)
        where TOwner : class
        where TTranslation : EntityTranslation
    {
        var owner = await query.Include(navigation).SingleAsync();
        var translations = navigation.Compile()(owner);
        Assert.Equal(1, Assert.Single(translations).LanguageId);
        await load(owner, default);
        Assert.Equal(new[] { 1, 2 }, translations.Select(row => row.LanguageId).Order().ToArray());
    }

    /// <summary>Hər sınaqda təzə context-lər arasında paylaşılan, ayrıca yaddaş bazası yaradır.</summary>
    private static DbContextOptions<ECafeDbContext> Options()
        => new DbContextOptionsBuilder<ECafeDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;

    /// <summary>Həqiqi repository-lərlə kateqoriya xidmətini restoran meneceri kontekstində qurur.</summary>
    private static CategoryManager Manager(ECafeDbContext db)
    {
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([
                new Claim(ClaimTypes.Role, ((int)RoleCode.Manager).ToString()),
                new Claim("restaurantId", "1")], "test"))
        };
        var mapper = new MapperConfiguration(configuration => configuration.AddProfile<CategoryProfile>(),
            NullLoggerFactory.Instance).CreateMapper();
        return new CategoryManager(new HttpContextAccessor { HttpContext = http }, mapper,
            new ConfigurationBuilder().Build(), new CategoryRepository(db), new RestaurantRepository(db),
            Mock.Of<IAuditLogService>());
    }

    /// <summary>Beş sahibin AZ və deaktiv EN sətirlərini, həmçinin başqa restoranın ayrıca tərcüməsini saxlayır.</summary>
    private static async Task SeedAsync(DbContextOptions<ECafeDbContext> options)
    {
        await using var db = new ECafeDbContext(options);
        db.Languages.AddRange(new Language { Id = 1, Code = "az", Name = "AZ" },
            new Language { Id = 2, Code = "en", Name = "EN", IsActive = false });
        var restaurant = new Restaurant { Id = 1, Name = "Cafe", Location = "Baku", Phone = "000", IsActive = true,
            Translations = [new() { LanguageId = 1, Name = "Cafe" }, new() { LanguageId = 2, Name = "English cafe" }] };
        var other = new Restaurant { Id = 2, Name = "Other", Location = "Baku", Phone = "001", IsActive = true };
        var category = new Category { Id = 7, Name = "Category", Slug = "category", Restaurant = restaurant,
            Translations = [new() { LanguageId = 1, Name = "Category" },
                new() { LanguageId = 2, Name = "Old English", Revision = 7 }] };
        db.AddRange(restaurant, other, category,
            new Category { Id = 8, Name = "Other", Slug = "other", Restaurant = other,
                Translations = [new() { LanguageId = 1, Name = "Other" }, new() { LanguageId = 2, Name = "Other English" }] },
            new Table { Id = 7, Restaurant = restaurant,
                Translations = [new() { LanguageId = 1, Name = "Table" }, new() { LanguageId = 2, Name = "English table" }] },
            new Item { Id = 7, Name = "Item", Restaurant = restaurant, Category = category,
                Translations = [new() { LanguageId = 1, Name = "Item" }, new() { LanguageId = 2, Name = "English item" }] },
            new InventoryItem { Id = 7, Name = "Stock", Restaurant = restaurant,
                Unit = new Unit { Id = 1, Name = "Piece", Code = "pcs" },
                Translations = [new() { LanguageId = 1, Name = "Stock" }, new() { LanguageId = 2, Name = "English stock" }] });
        await db.SaveChangesAsync();
    }
}
