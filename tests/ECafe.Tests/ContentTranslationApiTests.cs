using System.Data;
using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using ECafe.Application.Common.Exceptions;
using ECafe.Application.Common.Audit;
using ECafe.Application.DTOs.ContentLocalization;
using ECafe.Application.DTOs.Order;
using ECafe.Application.DTOs.Restaurant;
using ECafe.Application.DTOs.Restaurant.Public;
using ECafe.Application.DTOs.RestaurantGroup;
using ECafe.Application.DTOs.User;
using ECafe.Application.Mappings;
using ECafe.Application.Repositories.ContentLocalization;
using ECafe.Application.Repository;
using ECafe.Application.Services.ContentLocalization.Concrete;
using ECafe.Application.Services.AuditLog.Abstract;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using ECafe.Infrastructure.Context;
using ECafe.Infrastructure.Repositories.ContentLocalization;
using ECafe.Shared.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ECafe.Tests;

public sealed class ContentTranslationApiTests
{
    [Theory]
    [InlineData("restaurants", PermissionCode.EditRestaurantInfo)]
    [InlineData("restaurant-groups", PermissionCode.ManageRestaurants)]
    [InlineData("categories", PermissionCode.ManageCatalog)]
    [InlineData("items", PermissionCode.ManageCatalog)]
    [InlineData("tables", PermissionCode.ManageTables)]
    [InlineData("inventory-items", PermissionCode.ManageInventory)]
    [InlineData("units", PermissionCode.ManageInventory)]
    public async Task RevokedSourcePermissionDeniesEditorEvenForUnchangedSuperAdminRole(string type, PermissionCode permission)
    {
        var f = new Fixture { Actor = new ContentActor(1, 3, []), Source = new(7, null, new("Main")), PermissionGranted = false };
        f.Context.User = Principal(1);
        await Assert.ThrowsAsync<ForbiddenException>(() => f.Service.GetAsync(type, 7, default));
        await Assert.ThrowsAsync<ForbiddenException>(() => f.Service.SaveAsync(type, 7, "en", new() { ExpectedRevision = 0, Name = "Translated" }, default));
        await Assert.ThrowsAsync<ForbiddenException>(() => f.Service.DeleteAsync(type, 7, "en", 0, default));
        f.AccessRepository.Verify(repository => repository.HasPermissionAsync(1, permission, It.IsAny<CancellationToken>()), Times.Exactly(3));
        Assert.Equal(0, f.Writes);
        f.Audit.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ManagerPermissionRevocationIsRecheckedInsideWriteTransaction()
    {
        var f = new Fixture();
        f.Repository.Setup(repository => repository.AcquireLockAsync(It.IsAny<ContentEntityType>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Callback(() => f.PermissionGranted = false).Returns(Task.CompletedTask);
        await Assert.ThrowsAsync<ForbiddenException>(() => f.Service.SaveAsync("items", 7, "en", new() { ExpectedRevision = 0, Name = "Tea" }, default));
        Assert.Equal(0, f.Writes);
        f.AccessRepository.Verify(repository => repository.HasPermissionAsync(3, PermissionCode.ManageCatalog, It.IsAny<CancellationToken>()), Times.Exactly(2));
        f.Audit.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task PermissionCheckUsesLiveEffectiveAssignmentRoleNotGlobalRole()
    {
        var f = new Fixture { Actor = new ContentActor(3, 3, [new(1, 2)]) };
        f.Context.User = Principal(3, "1:2");
        await f.Service.GetAsync("items", 7, default);
        f.AccessRepository.Verify(repository => repository.HasPermissionAsync(2, PermissionCode.ManageCatalog, It.IsAny<CancellationToken>()), Times.Once);
        f.AccessRepository.Verify(repository => repository.HasPermissionAsync(3, It.IsAny<PermissionCode>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SuccessfulRestaurantTranslationWritesRecordAuditWithoutRawText()
    {
        var f = new Fixture();
        await f.Service.SaveAsync("items", 7, "en", new() { ExpectedRevision = 0, Name = "Private draft text" }, default);
        await f.Service.DeleteAsync("items", 7, "en", 1, default);
        f.Audit.Verify(service => service.RecordRestaurantActionAsync(1, ContentTranslationAuditActions.Updated,
            It.Is<object>(metadata => JsonSerializer.Serialize(metadata, (JsonSerializerOptions?)null).Contains("en") &&
                !JsonSerializer.Serialize(metadata, (JsonSerializerOptions?)null).Contains("Private draft text")), "Item", 7, null), Times.Once);
        f.Audit.Verify(service => service.RecordRestaurantActionAsync(1, ContentTranslationAuditActions.Deleted,
            It.IsAny<object>(), "Item", 7, null), Times.Once);
    }

    [Fact]
    public async Task EditorCreatesUpdatesDeletesAndRejectsStaleRevisionWithoutTouchingCanonical()
    {
        var f = new Fixture();
        var first = await f.Service.SaveAsync("items", 7, "en", new() { ExpectedRevision = 0, Name = " Tea " }, default);
        Assert.Equal("Tea", first.Translations.Single(row => row.LanguageCode == "en").Name);
        Assert.Equal("Cay", first.Translations.Single(row => row.LanguageCode == "az").Name);
        Assert.Null(first.Translations.Single(row => row.LanguageCode == "ru").Name);
        var second = await f.Service.SaveAsync("items", 7, "en", new() { ExpectedRevision = 1, Description = "Hot tea" }, default);
        Assert.Equal(2, second.Translations.Single(row => row.LanguageCode == "en").Revision);
        Assert.Null(second.Translations.Single(row => row.LanguageCode == "en").Name);
        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            f.Service.SaveAsync("items", 7, "en", new() { ExpectedRevision = 1, Name = "Old write" }, default));
        Assert.Equal(ErrorCode.LocalizationDraftConflict, exception.Code);
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.DeleteAsync("items", 7, "en", 1, default));
        await f.Service.DeleteAsync("items", 7, "en", 2, default);
        var cleared = Assert.Single(f.Rows[ContentEntityType.Item]);
        Assert.Equal(new ContentTranslationValue(), cleared.Value);
        Assert.Equal(3, cleared.Revision);
        Assert.Equal("Cay", f.Source!.Canonical.Name);
    }

    [Fact]
    public async Task DeletedAndRecreatedTranslationRejectsPreviousEditors()
    {
        var f = new Fixture();
        await f.Service.SaveAsync("items", 7, "en", new() { ExpectedRevision = 0, Name = "First" }, default);
        await f.Service.DeleteAsync("items", 7, "en", 1, default);
        var deleted = await f.Service.GetAsync("items", 7, default);
        var empty = deleted.Translations.Single(row => row.LanguageCode == "en");
        Assert.Null(empty.Name);
        Assert.Equal(2, empty.Revision);
        var recreated = await f.Service.SaveAsync("items", 7, "en", new() { ExpectedRevision = empty.Revision, Name = "New" }, default);
        Assert.Equal(3, recreated.Translations.Single(row => row.LanguageCode == "en").Revision);
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.SaveAsync("items", 7, "en",
            new() { ExpectedRevision = 1, Name = "Stale" }, default));
        Assert.Equal(ErrorCode.LocalizationDraftConflict, error.Code);
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.DeleteAsync("items", 7, "en", 1, default));
        Assert.Equal("New", Assert.Single(f.Rows[ContentEntityType.Item]).Value.Name);
    }

    [Theory]
    [InlineData("az")]
    [InlineData("de")]
    [InlineData("EN")]
    public async Task CanonicalAndUnsupportedLanguageCannotBeEditedSeparately(string language)
    {
        var f = new Fixture();
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.SaveAsync("items", 7, language,
            new() { ExpectedRevision = 0, Name = "Tea" }, default));
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.DeleteAsync("items", 7, language, 0, default));
        Assert.Equal(0, f.Writes);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(-1L)]
    [InlineData(long.MaxValue)]
    public async Task RevisionMustBeExplicitAndBounded(long? revision)
    {
        var f = new Fixture();
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.SaveAsync("items", 7, "en",
            new() { ExpectedRevision = revision, Name = "Tea" }, default));
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.DeleteAsync("items", 7, "en", revision, default));
        Assert.Equal(0, f.Writes);
    }

    [Fact]
    public async Task OtherRestaurantAndWaiterCannotReadOrWriteEditor()
    {
        var f = new Fixture { Actor = new ContentActor(3, 3, [new(2, 3)]) };
        await Assert.ThrowsAsync<ForbiddenException>(() => f.Service.GetAsync("items", 7, default));
        await Assert.ThrowsAsync<ForbiddenException>(() => f.Service.SaveAsync("items", 7, "en", new() { ExpectedRevision = 0, Name = "Tea" }, default));
        await Assert.ThrowsAsync<ForbiddenException>(() => f.Service.DeleteAsync("items", 7, "en", 0, default));
        f.Actor = new ContentActor(3, 3, [new(1, 4)]);
        await Assert.ThrowsAsync<ForbiddenException>(() => f.Service.GetAsync("items", 7, default));
        Assert.Equal(0, f.Writes);
    }

    [Fact]
    public async Task RoleSessionAndAssignmentAreRecheckedAfterLock()
    {
        var f = new Fixture { Actor = new ContentActor(4, 3, [new(1, 3)]) };
        await Assert.ThrowsAsync<ForbiddenException>(() => f.Service.GetAsync("items", 7, default));
        f.Actor = new ContentActor(3, 4, [new(1, 3)]);
        await Assert.ThrowsAsync<ForbiddenException>(() => f.Service.GetAsync("items", 7, default));
        f.Actor = new ContentActor(3, 3, [new(1, 3)]);
        f.Repository.Setup(repository => repository.AcquireLockAsync(It.IsAny<ContentEntityType>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Callback(() => f.Actor = null).Returns(Task.CompletedTask);
        await Assert.ThrowsAsync<ForbiddenException>(() => f.Service.SaveAsync("items", 7, "en", new() { ExpectedRevision = 0, Name = "Tea" }, default));
        Assert.Equal(0, f.Writes);
    }

    [Theory]
    [InlineData("units")]
    [InlineData("restaurant-groups")]
    public async Task GlobalContentRequiresLiveSuperAdmin(string entityType)
    {
        var f = new Fixture { Source = new ContentTranslationSource(7, null, new("Main")) };
        await Assert.ThrowsAsync<ForbiddenException>(() => f.Service.GetAsync(entityType, 7, default));
        f.Actor = new ContentActor(1, 3, []);
        f.Context.User = Principal(1);
        var response = await f.Service.SaveAsync(entityType, 7, "en", new() { ExpectedRevision = 0, Name = "Translated" }, default);
        Assert.Equal("Translated", response.Translations.Single(row => row.LanguageCode == "en").Name);
    }

    [Fact]
    public async Task DeletedSourceAndInactiveLanguageCannotBeWritten()
    {
        var f = new Fixture { Source = null };
        await Assert.ThrowsAsync<NotFoundException>(() => f.Service.SaveAsync("items", 7, "en", new() { ExpectedRevision = 0, Name = "Tea" }, default));
        f.Source = new(7, 1, new("Cay"));
        f.Languages = [new(1, "az", true), new(2, "en", false)];
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.SaveAsync("items", 7, "en", new() { ExpectedRevision = 0, Name = "Tea" }, default));
        Assert.Equal(0, f.Writes);
    }

    [Theory]
    [InlineData("<script>bad</script>")]
    [InlineData("Bad\0name")]
    [InlineData("")]
    public async Task InvalidTextCannotBeStored(string name)
    {
        var f = new Fixture();
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.SaveAsync("items", 7, "en", new() { ExpectedRevision = 0, Name = name }, default));
        Assert.Equal(0, f.Writes);
    }

    [Fact]
    public async Task FieldsAndLengthsAreLimitedByEntitySchema()
    {
        var f = new Fixture();
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.SaveAsync("items", 7, "en",
            new() { ExpectedRevision = 0, Name = new string('a', 151) }, default));
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.SaveAsync("tables", 7, "en",
            new() { ExpectedRevision = 0, Name = new string('a', 51) }, default));
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.SaveAsync("categories", 7, "en",
            new() { ExpectedRevision = 0, Name = "Category", Description = "Unsupported" }, default));
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.SaveAsync("items", 7, "en",
            new() { ExpectedRevision = 0, Name = "Tea", Location = "Unsupported" }, default));
        Assert.Equal(0, f.Writes);
    }

    [Fact]
    public async Task ReaderBatchesRepeatedIdsAndPreservesSparseFallbackAndPagination()
    {
        using var culture = new CultureScope("en");
        var f = new Fixture();
        f.Rows[ContentEntityType.Restaurant].Add(new(7, "en", 1, new(Name: "Cafe", Location: null)));
        var first = new PublicRestaurantListItemDto { Id = 7, Name = "Restoran", Location = "Baki" };
        var second = new PublicRestaurantListItemDto { Id = 7, Name = "Restoran", Location = "Baki" };
        var page = new PaginatedList<PublicRestaurantListItemDto>([first, second], 25, 2, 2);
        await new ContentTranslationReader(f.Repository.Object).LocalizeAsync(page, default);
        Assert.Equal("Cafe", first.Name);
        Assert.Equal("Cafe", second.Name);
        Assert.Equal("Baki", first.Location);
        Assert.Equal(25, page.TotalCount);
        Assert.Equal(2, page.PageIndex);
        f.Repository.Verify(repository => repository.GetRowsAsync(ContentEntityType.Restaurant,
            It.Is<IReadOnlyCollection<int>>(ids => ids.Count == 1 && ids.Contains(7)), "en", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReaderPreservesOrderSnapshotsPersonalLegalAndRawEditorData()
    {
        using var culture = new CultureScope("en");
        var f = new Fixture();
        f.Rows[ContentEntityType.Item].Add(new(7, "en", 1, new("Tea")));
        f.Rows[ContentEntityType.Restaurant].Add(new(7, "en", 1, new("Cafe")));
        f.Rows[ContentEntityType.RestaurantGroup].Add(new(7, "en", 1, new("Translated brand")));
        var order = new CustomerOrderLineResponse(1, 7, "Cay snapshot", 2, 5, 10);
        var kitchen = new KitchenOrderLineResponse(1, 7, "Cay snapshot", 2, "My note");
        var profile = new ProfileResponseDto { Id = 7, Name = "Ali", Surname = "Valiyev", RestaurantId = 7, RestaurantName = "Restoran" };
        var group = new RestaurantGroupResponse { Id = 7, Name = "Brand", LegalName = "Brand MMC" };
        var editor = new ContentTranslationsResponse("items", 7, 1, [new("az", "Cay", "Esas", null, null, null, 1)]);
        await new ContentTranslationReader(f.Repository.Object).LocalizeAsync(new object[] { order, kitchen, profile, group, editor }, default);
        Assert.Equal("Cay snapshot", order.Name);
        Assert.Equal("Cay snapshot", kitchen.Name);
        Assert.Equal("My note", kitchen.Note);
        Assert.Equal("Ali", profile.Name);
        Assert.Equal("Cafe", profile.RestaurantName);
        Assert.Equal("Translated brand", group.Name);
        Assert.Equal("Brand MMC", group.LegalName);
        Assert.Equal("Cay", editor.Translations[0].Name);
        f.Repository.Verify(repository => repository.GetRowsAsync(ContentEntityType.Item,
            It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void HttpTranslationEntryHasFlatCompatibleContract()
    {
        var json = JsonSerializer.Serialize(new ContentTranslationEntry("en", "Tea", "Hot", null, null, null, 3),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Contains("\"languageCode\":\"en\"", json);
        Assert.Contains("\"name\":\"Tea\"", json);
        Assert.Contains("\"revision\":3", json);
        Assert.DoesNotContain("\"value\"", json);
    }

    [Fact]
    public async Task RepositoryHonorsParentAndLanguageFiltersAndUpdatesCompositeRowInPlace()
    {
        await using var db = new ECafeDbContext(new DbContextOptionsBuilder<ECafeDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Languages.AddRange(new Language { Id = 1, Code = "az", Name = "AZ" }, new Language { Id = 2, Code = "en", Name = "EN" });
        var restaurant = new Restaurant { Id = 1, Name = "Cafe", Location = "Baku", Phone = "000" };
        var category = new Category { Id = 8, Name = "Main", Slug = "main", Restaurant = restaurant };
        db.Categories.Add(category);
        await db.SaveChangesAsync();
        var repository = new ContentTranslationRepository(db, [new CategoryTranslationStore(db)]);
        await repository.SaveAsync(ContentEntityType.Category, 8, 2, new("Dishes"), 1, default);
        await repository.SaveAsync(ContentEntityType.Category, 8, 2, new("New dishes"), 2, default);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => repository.SaveAsync(ContentEntityType.Category, 8, 2, new("Stale write"), 2, default));
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => repository.DeleteAsync(ContentEntityType.Category, 8, 2, 1, default));
        Assert.Equal(2, Assert.Single(await repository.GetRowsAsync(ContentEntityType.Category, [8], "en", default)).Revision);
        Assert.NotNull(await repository.GetSourceAsync(ContentEntityType.Category, 8, default));
        restaurant.IsDeleted = true;
        await db.SaveChangesAsync();
        Assert.Null(await repository.GetSourceAsync(ContentEntityType.Category, 8, default));
        Assert.Empty(await repository.GetRowsAsync(ContentEntityType.Category, [8], "en", default));
        Assert.Single(await db.CategoryTranslations.IgnoreQueryFilters().ToListAsync());
    }

    /// <summary>Canlı menecer rolunu, cihaz sessiyasını və sessionVersion iddiasını daşıyan sınaq principal-ı qurur.</summary>
    private static ClaimsPrincipal Principal(int role = 3, string restaurantRoles = "1:3") => new(new ClaimsIdentity([
        new Claim("userId", "11"), new Claim(ClaimTypes.Role, role.ToString()), new Claim("sessionVersion", "3"),
        new Claim("sessionId", "11111111111111111111111111111111"), new Claim("restaurantRoles", restaurantRoles)], "test"));

    private sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo previous = CultureInfo.CurrentUICulture;
        public CultureScope(string language) => CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(language);
        public void Dispose() => CultureInfo.CurrentUICulture = previous;
    }

    private sealed class Fixture
    {
        public Mock<IContentTranslationRepository> Repository { get; } = new();
        public Mock<IContentTranslationAccessRepository> AccessRepository { get; } = new();
        public Mock<IContentLanguageRepository> LanguageRepository { get; } = new();
        public Mock<IAuditLogService> Audit { get; } = new();
        public ContentTranslationManager Service { get; }
        public DefaultHttpContext Context { get; } = new() { User = Principal() };
        public ContentActor? Actor { get; set; } = new(3, 3, [new(1, 3)]);
        public ContentTranslationSource? Source { get; set; } = new(7, 1, new("Cay", "Esas metn"));
        public List<ContentLanguage> Languages { get; set; } = [new(1, "az", true), new(2, "en", true), new(3, "ru", true)];
        public Dictionary<ContentEntityType, List<ContentTranslationRow>> Rows { get; } = Enum.GetValues<ContentEntityType>().ToDictionary(type => type, _ => new List<ContentTranslationRow>());
        public int Writes { get; private set; }
        public bool PermissionGranted { get; set; } = true;

        public Fixture()
        {
            AccessRepository.Setup(repository => repository.GetActorAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => Actor);
            AccessRepository.Setup(repository => repository.HasActiveSessionAsync(11, "11111111111111111111111111111111", It.IsAny<CancellationToken>())).ReturnsAsync(true);
            AccessRepository.Setup(repository => repository.HasPermissionAsync(It.IsAny<int>(), It.IsAny<PermissionCode>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => PermissionGranted);
            Repository.Setup(repository => repository.GetSourceAsync(It.IsAny<ContentEntityType>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => Source);
            LanguageRepository.Setup(repository => repository.GetLanguagesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => Languages);
            Repository.Setup(repository => repository.GetRowsAsync(It.IsAny<ContentEntityType>(), It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((ContentEntityType type, IReadOnlyCollection<int> ids, string? language, CancellationToken _) =>
                    (IReadOnlyList<ContentTranslationRow>)Rows[type].Where(row => ids.Contains(row.Id) && (language == null || row.LanguageCode == language)).ToList());
            Repository.Setup(repository => repository.SaveAsync(It.IsAny<ContentEntityType>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<ContentTranslationValue>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
                .Callback((ContentEntityType type, int id, int languageId, ContentTranslationValue value, long revision, CancellationToken _) =>
                {
                    Writes++;
                    var language = Languages.Single(row => row.Id == languageId).Code;
                    Rows[type].RemoveAll(row => row.Id == id && row.LanguageCode == language);
                    Rows[type].Add(new(id, language, revision, value));
                }).Returns(Task.CompletedTask);
            Repository.Setup(repository => repository.DeleteAsync(It.IsAny<ContentEntityType>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
                .Callback((ContentEntityType type, int id, int languageId, long revision, CancellationToken _) =>
                {
                    Writes++;
                    var language = Languages.Single(row => row.Id == languageId).Code;
                    Rows[type].RemoveAll(row => row.Id == id && row.LanguageCode == language);
                    Rows[type].Add(new(id, language, checked(revision + 1), new ContentTranslationValue()));
                }).Returns(Task.CompletedTask);
            var transactions = new Mock<IApplicationDbTransactionFactory>();
            transactions.Setup(factory => factory.BeginTransactionAsync(It.IsAny<IsolationLevel>(), It.IsAny<CancellationToken>())).ReturnsAsync(Mock.Of<IApplicationDbTransaction>());
            var access = new ContentTranslationAccessManager(Repository.Object, AccessRepository.Object, new HttpContextAccessor { HttpContext = Context });
            Service = new ContentTranslationManager(Repository.Object, LanguageRepository.Object, access, transactions.Object, Audit.Object);
        }
    }
}
