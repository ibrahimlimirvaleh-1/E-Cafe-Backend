using ECafe.Application.DTOs.ContentLocalization;
using ECafe.Application.Repositories.ContentLocalization;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Infrastructure.Context;
using ECafe.Infrastructure;
using ECafe.Infrastructure.Repositories.ContentLocalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace ECafe.Tests;

public sealed class ContentTranslationStoreTests
{
    [Theory]
    [InlineData(ContentEntityType.Restaurant)]
    [InlineData(ContentEntityType.RestaurantGroup)]
    [InlineData(ContentEntityType.Category)]
    [InlineData(ContentEntityType.Item)]
    [InlineData(ContentEntityType.Table)]
    [InlineData(ContentEntityType.InventoryItem)]
    [InlineData(ContentEntityType.Unit)]
    public async Task EveryTypedStorePreservesLifecycleCompositeIdentityAndRevisionChecks(ContentEntityType type)
    {
        await using var db = CreateContext();
        await SeedAsync(db);
        var stores = CreateStores(db);
        var repository = new ContentTranslationRepository(db, stores);
        var canonical = await repository.GetSourceAsync(type, 7, default);
        Assert.NotNull(canonical);

        await repository.SaveAsync(type, 7, 2, new("English", "Description", "Location", "Branch", "Unavailable"), 1, default);
        await repository.SaveAsync(type, 7, 3, new("Russian"), 1, default);
        var trackedCount = db.ChangeTracker.Entries().Count();
        await repository.SaveAsync(type, 7, 2, new("Updated", "New description", "New location", "New branch", "New unavailable"), 2, default);
        Assert.Equal(trackedCount, db.ChangeTracker.Entries().Count());
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
            repository.SaveAsync(type, 7, 2, new("Stale"), 2, default));
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
            repository.DeleteAsync(type, 7, 2, 1, default));
        var row = Assert.Single(await repository.GetRowsAsync(type, [7, 99], "en", default));
        Assert.Equal("Updated", row.Value.Name);
        Assert.Equal(2, row.Revision);
        if (type == ContentEntityType.Item)
        {
            Assert.Equal("New description", row.Value.Description);
            Assert.Equal("New unavailable", row.Value.UnavailableReason);
        }
        if (type == ContentEntityType.Restaurant)
        {
            Assert.Equal("New location", row.Value.Location);
            Assert.Equal("New branch", row.Value.BranchName);
        }
        Assert.Equal(canonical, await repository.GetSourceAsync(type, 7, default));
        await repository.DeleteAsync(type, 7, 2, 2, default);
        var cleared = Assert.Single(await repository.GetRowsAsync(type, [7], "en", default));
        Assert.Equal(new ContentTranslationValue(), cleared.Value);
        Assert.Equal(3, cleared.Revision);
        await repository.SaveAsync(type, 7, 2, new("Recreated"), 4, default);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => repository.SaveAsync(type, 7, 2, new("Old editor"), 3, default));
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => repository.DeleteAsync(type, 7, 2, 2, default));
        await repository.DeleteAsync(type, 7, 2, 4, default);
        Assert.Single(await repository.GetRowsAsync(type, [7], "ru", default));

        db.Languages.Local.Single(language => language.Id == 3).IsActive = false;
        await db.SaveChangesAsync();
        Assert.Equal("en", Assert.Single(await repository.GetRowsAsync(type, [7], null, default)).LanguageCode);
        db.Languages.Local.Single(language => language.Id == 3).IsActive = true;
        db.ChangeTracker.Entries().Single(entry => entry.Metadata.ClrType.Name == type.ToString())
            .Property("IsDeleted").CurrentValue = true;
        await db.SaveChangesAsync();
        Assert.Null(await repository.GetSourceAsync(type, 7, default));
        Assert.Empty(await repository.GetRowsAsync(type, [7], null, default));
    }

    [Theory]
    [InlineData(ContentEntityType.Restaurant, "restaurant_translations")]
    [InlineData(ContentEntityType.RestaurantGroup, "restaurant_group_translations")]
    [InlineData(ContentEntityType.Category, "category_translations")]
    [InlineData(ContentEntityType.Item, "item_translations")]
    [InlineData(ContentEntityType.Table, "table_translations")]
    [InlineData(ContentEntityType.InventoryItem, "inventory_item_translations")]
    [InlineData(ContentEntityType.Unit, "unit_translations")]
    public void EveryTypedStoreProjectsAndFiltersOnPostgreSqlServer(ContentEntityType type, string tableName)
    {
        using var db = new ECafeDbContext(new DbContextOptionsBuilder<ECafeDbContext>()
            .UseNpgsql("Host=localhost;Database=translation_sql_only;Username=unused;Password=unused").Options);
        var store = CreateStores(db).Single(candidate => candidate.Type == type);
        var rowsSql = store.RowsQuery([7, 8], "en").ToQueryString();
        var sourceSql = store.SourceQuery(7).ToQueryString();
        Assert.Contains(tableName, rowsSql);
        Assert.Contains("WHERE", rowsSql);
        Assert.Contains("IsDeleted", rowsSql);
        Assert.Contains("IsActive", rowsSql);
        Assert.Contains("en", rowsSql);
        Assert.Contains("7", sourceSql);
        Assert.Contains("IsDeleted", sourceSql);
        Assert.Equal(QueryTrackingBehavior.TrackAll, db.ChangeTracker.QueryTrackingBehavior);
    }

    [Fact]
    public async Task UnsupportedKindIsRejectedEvenWithEmptyReadOrBeforeLockSql()
    {
        await using var db = CreateContext();
        var repository = new ContentTranslationRepository(db, CreateStores(db));
        var type = (ContentEntityType)999;
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => repository.GetSourceAsync(type, 7, default));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => repository.GetRowsAsync(type, [], null, default));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => repository.AcquireLockAsync(type, 7, default));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => repository.SaveAsync(type, 7, 2, new("Name"), 1, default));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => repository.DeleteAsync(type, 7, 2, 1, default));
    }

    [Fact]
    public async Task RegisteredStoreExtendsRepositoryWithoutChangingDispatchCode()
    {
        await using var db = CreateContext();
        var type = (ContentEntityType)999;
        var store = new Mock<IContentTranslationStore>();
        store.SetupGet(value => value.Type).Returns(type);
        store.Setup(value => value.SaveAsync(7, 2, It.IsAny<ContentTranslationValue>(), 1, default)).Returns(Task.CompletedTask);
        var repository = new ContentTranslationRepository(db, [store.Object]);
        await repository.SaveAsync(type, 7, 2, new("Extension"), 1, default);
        store.Verify(value => value.SaveAsync(7, 2, It.IsAny<ContentTranslationValue>(), 1, default), Times.Once);
        Assert.Empty(await repository.GetRowsAsync(type, [], "en", default));
        store.Verify(value => value.RowsQuery(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public void DuplicateRegistrationFailsInsteadOfSilentlyPickingStore()
    {
        using var db = CreateContext();
        Assert.Throws<ArgumentException>(() => new ContentTranslationRepository(db,
            [new ItemTranslationStore(db), new ItemTranslationStore(db)]));
    }

    [Fact]
    public void QueryAndWriteContractsResolveSameScopedRepositoryWithAllStores()
    {
        var services = new ServiceCollection();
        services.AddDistributedMemoryCache();
        services.AddInfrastructure(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:ECafeDb"] = "Host=localhost;Database=translation_di_only;Username=unused;Password=unused"
        }).Build());
        using var provider = services.BuildServiceProvider();
        using var first = provider.CreateScope();
        var write = first.ServiceProvider.GetRequiredService<IContentTranslationRepository>();
        Assert.Same(write, first.ServiceProvider.GetRequiredService<IContentTranslationQuery>());
        Assert.Equal(7, first.ServiceProvider.GetServices<IContentTranslationStore>().Select(store => store.Type).Distinct().Count());
        Assert.IsType<ContentTranslationAccessRepository>(first.ServiceProvider.GetRequiredService<IContentTranslationAccessRepository>());
        Assert.IsType<ContentLanguageRepository>(first.ServiceProvider.GetRequiredService<IContentLanguageRepository>());
        using var second = provider.CreateScope();
        Assert.NotSame(write, second.ServiceProvider.GetRequiredService<IContentTranslationQuery>());
    }

    [Fact]
    public async Task NarrowLanguageRepositoryIncludesInactiveLanguageForWriteValidation()
    {
        await using var db = CreateContext();
        db.Languages.Add(new Language { Id = 2, Code = "en", Name = "English", IsActive = false });
        await db.SaveChangesAsync();
        var row = Assert.Single(await new ContentLanguageRepository(db).GetLanguagesAsync(default));
        Assert.False(row.IsActive);
        Assert.Equal("en", row.Code);
    }

    [Fact]
    public async Task NarrowAccessRepositoryReadsLiveAssignmentsAndRevokedPermission()
    {
        await using var db = CreateContext();
        var role = new Role { Id = 3, Name = "Manager" };
        var permission = new Permission { Id = (int)PermissionCode.ManageCatalog, Name = "ManageCatalog" };
        var grant = new RolePermission { Id = 8, Role = role, Permission = permission };
        var restaurant = new Restaurant { Id = 7, Name = "Cafe", Location = "Baku", Phone = "000" };
        var user = new User { Id = 11, Name = "Test", Surname = "User", Email = "test@example.test", Phone = "000", Password = "test-only", Role = role, IsActive = true, SessionVersion = 4 };
        var assignment = new UserRestaurant { User = user, Restaurant = restaurant, Role = role, IsActive = true };
        db.AddRange(grant, assignment);
        await db.SaveChangesAsync();
        var repository = new ContentTranslationAccessRepository(db);
        var actor = await repository.GetActorAsync(11, default);
        Assert.NotNull(actor);
        Assert.Equal(4, actor.SessionVersion);
        Assert.Equal(7, Assert.Single(actor.Assignments).RestaurantId);
        Assert.True(await repository.HasPermissionAsync(3, PermissionCode.ManageCatalog, default));
        grant.IsDeleted = true;
        assignment.IsActive = false;
        await db.SaveChangesAsync();
        Assert.False(await repository.HasPermissionAsync(3, PermissionCode.ManageCatalog, default));
        Assert.Empty((await repository.GetActorAsync(11, default))!.Assignments);
        user.IsActive = false;
        await db.SaveChangesAsync();
        Assert.Null(await repository.GetActorAsync(11, default));
    }

    [Fact]
    public async Task ConcurrentContextWriteStillUsesEfRevisionToken()
    {
        var options = new DbContextOptionsBuilder<ECafeDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var first = new ECafeDbContext(options);
        await SeedAsync(first);
        var firstStore = new CategoryTranslationStore(first);
        await firstStore.SaveAsync(7, 2, new("First"), 1, default);
        await using var second = new ECafeDbContext(options);
        await new CategoryTranslationStore(second).SaveAsync(7, 2, new("Concurrent"), 2, default);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => firstStore.SaveAsync(7, 2, new("Stale"), 2, default));
        Assert.Equal("Concurrent", (await second.CategoryTranslations.AsNoTracking().SingleAsync()).Name);
    }

    /// <summary>Hər sınağa müstəqil yaddaş bazası verir; istehsal bağlantısı açılmır.</summary>
    private static ECafeDbContext CreateContext()
        => new(new DbContextOptionsBuilder<ECafeDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    /// <summary>İstehsaldakı yeddi tipli saxlayıcını test üçün eyni context üzərində qurur.</summary>
    private static IContentTranslationStore[] CreateStores(ECafeDbContext db)
        => [new RestaurantTranslationStore(db), new RestaurantGroupTranslationStore(db), new CategoryTranslationStore(db),
            new ItemTranslationStore(db), new TableTranslationStore(db), new InventoryItemTranslationStore(db), new UnitTranslationStore(db)];

    /// <summary>Bütün tərcümə növlərinin əsas mənbələrini və dil əlaqələrini eyni identifikatorla hazırlayır.</summary>
    private static async Task SeedAsync(ECafeDbContext db)
    {
        db.Languages.AddRange(new Language { Id = 1, Code = "az", Name = "AZ" },
            new Language { Id = 2, Code = "en", Name = "EN" }, new Language { Id = 3, Code = "ru", Name = "RU" });
        var group = new RestaurantGroup { Id = 7, Name = "Group" };
        var restaurant = new Restaurant { Id = 7, Name = "Restaurant", Location = "Baku", Phone = "000", RestaurantGroup = group };
        var category = new Category { Id = 7, Name = "Category", Slug = "category", Restaurant = restaurant };
        var unit = new Unit { Id = 7, Name = "Unit", Code = "u" };
        db.AddRange(group, restaurant, category, unit,
            new Item { Id = 7, Name = "Item", Restaurant = restaurant, Category = category },
            new Table { Id = 7, Name = "Table", Restaurant = restaurant },
            new InventoryItem { Id = 7, Name = "Inventory", Restaurant = restaurant, Unit = unit });
        await db.SaveChangesAsync();
    }
}
