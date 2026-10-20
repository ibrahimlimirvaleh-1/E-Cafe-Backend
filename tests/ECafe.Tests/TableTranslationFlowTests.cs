using System.Data;
using System.Security.Claims;
using System.Text.Json;
using AutoMapper;
using ECafe.Api.Controllers;
using ECafe.Api.Filters;
using ECafe.Application.DTOs.ContentLocalization;
using ECafe.Application.DTOs.Table;
using ECafe.Application.Features.Queries.Table;
using ECafe.Application.Mappings;
using ECafe.Application.Repositories.ContentLocalization;
using ECafe.Application.Repositories.Restaurant;
using ECafe.Application.Repository;
using ECafe.Application.Services.AuditLog.Abstract;
using ECafe.Application.Services.ContentLocalization.Concrete;
using ECafe.Application.Services.Localization;
using ECafe.Application.Services.Localization.Concrete;
using ECafe.Application.Services.Restaurant.Abstract;
using ECafe.Application.Services.Table.Abstract;
using ECafe.Application.Services.Table.Concrete;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Infrastructure.Context;
using ECafe.Infrastructure.Repositories.ContentLocalization;
using ECafe.Infrastructure.Repositories.Table;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace ECafe.Tests;

public sealed class TableTranslationFlowTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InlineTranslationsSurviveTableControllerSaveAndFreshLocalizedGet(bool create)
    {
        var options = await SeedAsync();
        int id;
        await using (var db = new ECafeDbContext(options))
        {
            var http = CreateHttpContext();
            var services = new ServiceCollection().AddSingleton<ITableService>(CreateTableManager(db, http));
            services.AddMediatR(configuration => configuration.RegisterServicesFromAssemblyContaining<GetRestaurantTablesQuery>());
            using var provider = services.BuildServiceProvider();
            http.RequestServices = provider;
            var controller = new TableController { ControllerContext = new ControllerContext { HttpContext = http } };
            List<TableTranslationRequest> translations = [new() { LanguageCode = "en", Name = "Table-1" }];
            var result = Assert.IsType<OkObjectResult>(create
                ? await controller.CreateTable(1, new CreateTableRequest { TableNo = 3, Name = "Masa-3", Capacity = 2, Translations = translations })
                : await controller.UpdateTable(1, 7, new UpdateTableRequest { TableNo = 1, Name = "Masa-1", Capacity = 2, IsActive = true, Translations = translations }));
            id = create ? Assert.IsType<int>(result.Value) : Assert.IsType<TableResponse>(result.Value).Id;
        }
        var en = (await GetTablesAsync(options, "en")).EnumerateArray().Single(row => row.GetProperty("id").GetInt32() == id);
        Assert.Equal("Table-1", en.GetProperty("name").GetString());
        var az = (await GetTablesAsync(options, "az")).EnumerateArray().Single(row => row.GetProperty("id").GetInt32() == id);
        Assert.Equal(create ? "Masa-3" : "Masa-1", az.GetProperty("name").GetString());
    }

    [Theory]
    [InlineData("en", "en", "Table-1")]
    [InlineData("en", "en-US,en;q=0.9", "Table-1")]
    [InlineData("ru", "ru", "Stol-1")]
    public async Task SavedTranslationReachesFreshTableControllerListAndAzRemainsCanonical(
        string language, string acceptLanguage, string translatedName)
    {
        var options = await SeedAsync();
        await using (var db = new ECafeDbContext(options))
        {
            var saved = await SaveTranslationAsync(db, language, translatedName, 0);
            Assert.Equal(translatedName, saved.GetProperty("translations").EnumerateArray()
                .Single(row => row.GetProperty("languageCode").GetString() == language).GetProperty("name").GetString());
            Assert.Equal("Masa-1", saved.GetProperty("translations").EnumerateArray()
                .Single(row => row.GetProperty("languageCode").GetString() == "az").GetProperty("name").GetString());
        }

        var localized = await GetTablesAsync(options, acceptLanguage);
        Assert.Equal(translatedName, localized[0].GetProperty("name").GetString());
        Assert.Equal("Masa-2", localized[1].GetProperty("name").GetString());
        Assert.Equal(7, localized[0].GetProperty("id").GetInt32());
        Assert.Equal(1, localized[0].GetProperty("restaurantId").GetInt32());
        Assert.Equal(2, localized[0].GetProperty("capacity").GetInt32());
        Assert.True(localized[0].GetProperty("isEmpty").GetBoolean());
        Assert.True(localized[0].GetProperty("isActive").GetBoolean());
        var az = await GetTablesAsync(options, "az");
        Assert.Equal("Masa-1", az[0].GetProperty("name").GetString());
        Assert.Equal("Masa-1", (await GetTablesAsync(options, null))[0].GetProperty("name").GetString());
        await using var verification = new ECafeDbContext(options);
        Assert.Equal("Masa-1", (await verification.Tables.SingleAsync(row => row.Id == 7)).Name);
        Assert.Equal(translatedName, (await verification.TableTranslations.SingleAsync()).Name);
    }

    [Fact]
    public async Task CanonicalTableUpdatePreservesSavedEnglishTranslationInSubsequentLists()
    {
        var options = await SeedAsync();
        await using (var db = new ECafeDbContext(options))
            await SaveTranslationAsync(db, "en", "Table-1", 0);

        await using (var db = new ECafeDbContext(options))
        {
            var context = CreateHttpContext();
            var manager = CreateTableManager(db, context);
            var services = new ServiceCollection().AddSingleton<ITableService>(manager);
            services.AddMediatR(configuration => configuration.RegisterServicesFromAssemblyContaining<GetRestaurantTablesQuery>());
            using var provider = services.BuildServiceProvider();
            context.RequestServices = provider;
            var controller = new TableController { ControllerContext = new ControllerContext { HttpContext = context } };
            await controller.UpdateTable(1, 7, new UpdateTableRequest
            {
                TableNo = 1, Name = "Yeni masa", Capacity = 4, IsActive = true
            });
        }

        var en = await GetTablesAsync(options, "en");
        Assert.Equal("Table-1", en[0].GetProperty("name").GetString());
        Assert.Equal(4, en[0].GetProperty("capacity").GetInt32());
        Assert.Equal("Yeni masa", (await GetTablesAsync(options, "az"))[0].GetProperty("name").GetString());
    }

    [Fact]
    public async Task UpdatedTranslationIsImmediatelyVisibleAndInactiveLanguageFallsBack()
    {
        var options = await SeedAsync();
        await using (var db = new ECafeDbContext(options))
            await SaveTranslationAsync(db, "en", "Table-1", 0);
        Assert.Equal("Table-1", (await GetTablesAsync(options, "en"))[0].GetProperty("name").GetString());
        await using (var db = new ECafeDbContext(options))
            await SaveTranslationAsync(db, "en", "Window table", 1);
        Assert.Equal("Window table", (await GetTablesAsync(options, "en"))[0].GetProperty("name").GetString());
        await using (var db = new ECafeDbContext(options))
        {
            (await db.Languages.SingleAsync(row => row.Code == "en")).IsActive = false;
            await db.SaveChangesAsync();
        }
        Assert.Equal("Masa-1", (await GetTablesAsync(options, "en"))[0].GetProperty("name").GetString());
    }

    /// <summary>Masa və dilləri real EF yaddaş bazasında saxlayır; hər sorğu eyni bazanı yeni kontekstdən oxuyur.</summary>
    private static async Task<DbContextOptions<ECafeDbContext>> SeedAsync()
    {
        var options = new DbContextOptionsBuilder<ECafeDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new ECafeDbContext(options);
        db.Languages.AddRange(new Language { Id = 1, Code = "az", Name = "AZ", IsDefault = true },
            new Language { Id = 2, Code = "en", Name = "EN" }, new Language { Id = 3, Code = "ru", Name = "RU" });
        db.Restaurants.Add(new Restaurant { Id = 1, Name = "Cafe", Location = "Baku", Phone = "000" });
        db.Tables.AddRange(new Table { Id = 7, RestaurantId = 1, TableNo = 1, Name = "Masa-1", Capacity = 2, IsActive = true },
            new Table { Id = 8, RestaurantId = 1, TableNo = 2, Name = "Masa-2", Capacity = 4, IsActive = true });
        await db.SaveChangesAsync();
        return options;
    }

    /// <summary>Real redaktor servisini və saxlayıcını çağırır; yalnız PostgreSQL kilidi və transaction testdə əvəz olunur.</summary>
    private static Task<JsonElement> SaveTranslationAsync(ECafeDbContext db, string language, string name, long revision)
    {
        var repository = new InMemoryTranslationRepository(CreateRepository(db));
        var http = CreateHttpContext();
        var accessor = new HttpContextAccessor { HttpContext = http };
        var accessRepository = new Mock<IContentTranslationAccessRepository>();
        accessRepository.Setup(row => row.GetActorAsync(11, It.IsAny<CancellationToken>())).ReturnsAsync(new ContentActor(1, 3, []));
        accessRepository.Setup(row => row.HasActiveSessionAsync(11, It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        accessRepository.Setup(row => row.HasPermissionAsync(1, PermissionCode.ManageTables, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var access = new ContentTranslationAccessManager(repository, accessRepository.Object, accessor);
        var transactions = new Mock<IApplicationDbTransactionFactory>();
        transactions.Setup(row => row.BeginTransactionAsync(It.IsAny<IsolationLevel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<IApplicationDbTransaction>());
        var manager = new ContentTranslationManager(repository, new ContentLanguageRepository(db), access, transactions.Object);
        var controller = new ContentTranslationsController(manager);
        return ExecuteLocalizedAsync(http, language, controller, () => controller.Save("tables", 7, language,
            new SaveContentTranslationRequest { Name = name, ExpectedRevision = revision }, default), repository);
    }

    /// <summary>Əsl mediator, masa servisi, repository və cavab filtrindən keçən GET-i yeni EF kontekstində icra edir.</summary>
    private static async Task<JsonElement> GetTablesAsync(DbContextOptions<ECafeDbContext> options, string? acceptLanguage)
    {
        await using var db = new ECafeDbContext(options);
        var http = CreateHttpContext();
        var services = new ServiceCollection().AddSingleton<ITableService>(CreateTableManager(db, http));
        services.AddMediatR(configuration => configuration.RegisterServicesFromAssemblyContaining<GetRestaurantTablesQuery>());
        using var provider = services.BuildServiceProvider();
        http.RequestServices = provider;
        var controller = new TableController { ControllerContext = new ControllerContext { HttpContext = http } };
        return await ExecuteLocalizedAsync(http, acceptLanguage, controller, () => controller.GetByRestaurant(1), CreateRepository(db));
    }

    /// <summary>Accept-Language middleware ilə dili seçir, nəticəni həqiqi filtrdə lokallaşdırıb HTTP JSON formasında yoxlamaya verir.</summary>
    private static async Task<JsonElement> ExecuteLocalizedAsync(DefaultHttpContext http, string? acceptLanguage,
        object controller, Func<Task<IActionResult>> action, IContentTranslationQuery repository)
    {
        if (acceptLanguage != null) http.Request.Headers.AcceptLanguage = acceptLanguage;
        JsonElement json = default;
        var options = new RequestLocalizationOptions();
        options.SetDefaultCulture("az").AddSupportedCultures("az", "en", "ru").AddSupportedUICultures("az", "en", "ru");
        options.RequestCultureProviders = [new AcceptLanguageHeaderRequestCultureProvider()];
        var middleware = new RequestLocalizationMiddleware(async context =>
        {
            var result = Assert.IsType<OkObjectResult>(await action());
            var route = new RouteData();
            route.Values["restaurantId"] = 1;
            route.Values["controller"] = controller is TableController ? "Table" : "ContentTranslations";
            route.Values["action"] = controller is TableController ? "GetByRestaurant" : "Save";
            var actionContext = new ActionContext(context, route, new ActionDescriptor());
            var text = new TextLocalizer(new HttpContextAccessor { HttpContext = context }, new TranslationBootstrap());
            json = await FilterResultAsync(actionContext, controller, result,
                new LocalizedResponseFilter(text, new ContentTranslationReader(repository)));
        }, Options.Create(options), NullLoggerFactory.Instance);
        await middleware.Invoke(http);
        return json;
    }

    /// <summary>MVC nəticə filtrinin davamını icra edib lokallaşdırılmış cavabı serializasiya edir.</summary>
    private static async Task<JsonElement> FilterResultAsync(ActionContext context, object controller,
        ObjectResult result, LocalizedResponseFilter filter)
    {
        JsonElement json = default;
        await filter.OnResultExecutionAsync(new ResultExecutingContext(context, [], result, controller), () =>
        {
            json = JsonSerializer.SerializeToElement(result.Value, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            return Task.FromResult(new ResultExecutedContext(context, [], result, controller));
        });
        return json;
    }

    /// <summary>İcazəli admin principal-ını qurur; canlı redaktor icazəsi ayrıca access manager tərəfindən yoxlanılır.</summary>
    private static DefaultHttpContext CreateHttpContext() => new()
    {
        User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("userId", "11"), new Claim(ClaimTypes.Role, "1"),
            new Claim("sessionVersion", "3"), new Claim("sessionId", "11111111111111111111111111111111")], "test"))
    };

    /// <summary>List və update üçün istehsal masa servisini real EF repository ilə qurur.</summary>
    private static TableManager CreateTableManager(ECafeDbContext db, HttpContext http) => new(
        new HttpContextAccessor { HttpContext = http }, new MapperConfiguration(configuration => configuration.AddProfile<TableProfile>(), NullLoggerFactory.Instance).CreateMapper(),
        new ConfigurationBuilder().Build(), new TableRepository(db), Mock.Of<IAuditLogService>(),
        Mock.Of<IRestaurantRepository>(), Mock.Of<IRestaurantDepositService>());

    /// <summary>Tərcümə yazı və oxumalarını istehsal masa saxlayıcısına bağlayır.</summary>
    private static ContentTranslationRepository CreateRepository(ECafeDbContext db) => new(db, [new TableTranslationStore(db)]);

    private sealed class InMemoryTranslationRepository(ContentTranslationRepository inner) : IContentTranslationRepository
    {
        /// <summary>Yaddaş provayderində PostgreSQL advisory kilidini buraxır; bütün məzmun əməliyyatları real repository-də qalır.</summary>
        public Task AcquireLockAsync(ContentEntityType type, int id, CancellationToken cancellationToken) => Task.CompletedTask;
        /// <summary>Əsas məzmunu real EF sorğusundan oxuyur.</summary>
        public Task<ContentTranslationSource?> GetSourceAsync(ContentEntityType type, int id, CancellationToken cancellationToken)
            => inner.GetSourceAsync(type, id, cancellationToken);
        /// <summary>Dil və silinmə filtrləri ilə tərcümələri real saxlayıcıdan oxuyur.</summary>
        public Task<IReadOnlyList<ContentTranslationRow>> GetRowsAsync(ContentEntityType type, IReadOnlyCollection<int> ids, string? language, CancellationToken cancellationToken)
            => inner.GetRowsAsync(type, ids, language, cancellationToken);
        /// <summary>Reviziya yoxlaması və EF saxlamasını istehsal repository-yə ötürür.</summary>
        public Task SaveAsync(ContentEntityType type, int id, int languageId, ContentTranslationValue value, long revision, CancellationToken cancellationToken)
            => inner.SaveAsync(type, id, languageId, value, revision, cancellationToken);
        /// <summary>Tərcümə silinməsini real repository-yə ötürür.</summary>
        public Task DeleteAsync(ContentEntityType type, int id, int languageId, long expectedRevision, CancellationToken cancellationToken)
            => inner.DeleteAsync(type, id, languageId, expectedRevision, cancellationToken);
    }
}
