using System.Data;
using System.Security.Claims;
using System.Text.Json;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using AutoMapper;
using ECafe.Application.Common.Errors;
using ECafe.Application.Common.Outbox;
using ECafe.Application.Common.Exceptions;
using ECafe.Application.DTOs.Localization;
using ECafe.Application.DTOs.Notification;
using ECafe.Application.DTOs.Reservation;
using ECafe.Application.DTOs.Workflow;
using ECafe.Api.Controllers;
using ECafe.Api.Filters;
using ECafe.Application.Features.Localization;
using ECafe.Application.Repositories.Localization;
using ECafe.Application.Repository;
using ECafe.Application.Services.Localization;
using ECafe.Application.Services.Localization.Concrete;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using ECafe.Domain.Policies;
using ECafe.Infrastructure.Context;
using ECafe.Infrastructure.Migrations.Seeds;
using ECafe.Infrastructure.Repositories.Localization;
using ECafe.Infrastructure.Repositories.User;
using ECafe.Infrastructure.Services.Localization;
using ECafe.Infrastructure.Services;
using ECafe.Application.Services.Localization.Abstract;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using MediatR;
using Moq;
using Xunit;

namespace ECafe.Tests;

public sealed class TranslationCatalogTests
{
    private const string Key = "api.error.InvalidCredentials";

    [Fact]
    public void FrozenRelationalSeedIsEmbeddedAndDoesNotDependOnFutureBootstrapEdits()
    {
        var sql = RelationalTranslationCatalogSeed.ReadSql();
        Assert.Equal("4142187ADF346A66485ABF2AFA501AC2F3C3C9A1BB4397886C913789356FA2E9",
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sql))));
        Assert.Contains("keywords: 2120; translations: 6360", sql);
        Assert.Contains("ON CONFLICT (\"Key\") DO NOTHING", sql);
        Assert.Contains("ON CONFLICT (\"KeywordId\", \"LanguageId\") DO NOTHING", sql);
    }

    [Fact]
    public void BundledCatalogHasAllErrorCodesInThreeLanguagesWithMatchingPlaceholders()
    {
        var bootstrap = new TranslationBootstrap();
        foreach (var code in ErrorMessageProvider.DefaultMessages.Keys)
        {
            var entry = bootstrap.Snapshot.Entries[$"api.error.{code}"];
            TranslationPolicy.ValidateText(entry);
            Assert.False(string.IsNullOrWhiteSpace(entry.Az));
            Assert.False(string.IsNullOrWhiteSpace(entry.En));
            Assert.False(string.IsNullOrWhiteSpace(entry.Ru));
        }
        Assert.NotEqual(bootstrap.Snapshot.ForLanguage("az").Messages[Key], bootstrap.Snapshot.ForLanguage("en").Messages[Key]);
        Assert.NotEqual(bootstrap.Snapshot.ForLanguage("en").Messages[Key], bootstrap.Snapshot.ForLanguage("ru").Messages[Key]);
    }

    [Fact]
    public async Task DraftIsInvisibleUntilPublicationAndPublishInvalidatesWarmCache()
    {
        await using var f = await Fixture.CreateAsync();
        var initial = await f.Service.GetPublishedAsync(default);
        var draft = await f.Service.SaveDraftAsync(Request(0, "Yeni mətn", "New text", "Новый текст"), default);
        Assert.Equal(1, draft.Revision);
        Assert.Equal(initial.Version, (await f.Service.GetPublishedAsync(default)).Version);
        Assert.NotEqual("New text", (await f.Service.GetPublishedAsync(default)).ForLanguage("en").Messages[Key]);
        var release = await f.Service.PublishAsync(1, null, default);
        var published = await f.Service.GetPublishedAsync(default);
        Assert.NotEqual(initial.Version, published.Version);
        Assert.Equal("New text", published.ForLanguage("en").Messages[Key]);
        Assert.Equal(2, release.Revision);
        Assert.Equal(1, release.CreatedByUserId);
    }

    [Fact]
    public async Task StaleEditorCannotOverwriteOrPublishNewerDraft()
    {
        await using var f = await Fixture.CreateAsync();
        await f.Service.SaveDraftAsync(Request(0), default);
        var save = await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.SaveDraftAsync(Request(0), default));
        Assert.Equal(ErrorCode.LocalizationDraftConflict, save.Code);
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.PublishAsync(0, null, default));
        Assert.Empty(f.Db.TranslationReleases);
    }

    [Fact]
    public async Task RollbackCreatesNewReleaseAndDoesNotRewriteHistory()
    {
        await using var f = await Fixture.CreateAsync();
        await f.Service.SaveDraftAsync(Request(0, "Bir", "One", "Один"), default);
        var first = await f.Service.PublishAsync(1, null, default);
        await f.Service.SaveDraftAsync(Request(2, "İki", "Two", "Два"), default);
        await f.Service.PublishAsync(3, null, default);
        var rollback = await f.Service.PublishAsync(4, first.Id, default);
        Assert.True(rollback.Id > first.Id);
        Assert.Equal(first.Id, rollback.SourceReleaseId);
        Assert.Equal("One", (await f.Service.GetPublishedAsync(default)).ForLanguage("en").Messages[Key]);
        Assert.Equal(3, await f.Db.TranslationReleases.CountAsync());
        var history = await f.Service.GetReleasesAsync(default);
        Assert.Equal(3, history.Count);
        Assert.Equal(rollback.Id, history[0].Id);
        Assert.Equal(first.Id, history[0].SourceReleaseId);
        Assert.Equal("One", (await new TranslationRepository(f.Db).GetReleaseEntriesAsync(first.Id, default))[Key].En);
    }

    [Theory]
    [InlineData(RoleCode.Customer)]
    [InlineData(RoleCode.Waiter)]
    [InlineData(RoleCode.Manager)]
    [InlineData(RoleCode.Owner)]
    public async Task RestaurantAndCustomerRolesCannotReadOrWriteDraft(RoleCode role)
    {
        await using var f = await Fixture.CreateAsync();
        f.Actor(2, role);
        await Assert.ThrowsAsync<ForbiddenException>(() => f.Service.GetDraftAsync(default));
        await Assert.ThrowsAsync<ForbiddenException>(() => f.Service.SaveDraftAsync(Request(0), default));
        await Assert.ThrowsAsync<ForbiddenException>(() => f.Service.PublishAsync(0, null, default));
        await Assert.ThrowsAsync<ForbiddenException>(() => f.Service.GetReleasesAsync(default));
    }

    [Fact]
    public async Task RevokedAdministratorIsRecheckedAfterWaitingForLock()
    {
        await using var f = await Fixture.CreateAsync();
        f.BeforeLock = async () =>
        {
            (await f.Db.Users.SingleAsync(user => user.Id == 1)).RoleId = (int)RoleCode.Customer;
            await f.Db.SaveChangesAsync();
        };
        await Assert.ThrowsAsync<ForbiddenException>(() => f.Service.SaveDraftAsync(Request(0), default));
        Assert.Equal(0, (await f.Db.TranslationCatalogs.SingleAsync()).Revision);
    }

    [Fact]
    public async Task LanguagePreferenceBelongsOnlyToCurrentUserAndDoesNotInvalidateSessions()
    {
        await using var f = await Fixture.CreateAsync();
        f.Actor(2, RoleCode.Customer);
        Assert.Equal("az", (await f.Service.GetPreferenceAsync(default)).Language);
        await f.Service.SavePreferenceAsync("ru", default);
        Assert.Equal("ru", (await f.Db.Users.SingleAsync(user => user.Id == 2)).PreferredLanguage);
        Assert.Equal("az", (await f.Db.Users.SingleAsync(user => user.Id == 1)).PreferredLanguage);
        Assert.Equal(0, (await f.Db.Users.SingleAsync(user => user.Id == 2)).SessionVersion);
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.SavePreferenceAsync("xx", default));
    }

    [Theory]
    [InlineData("<script>alert(1)</script>", "Text", "Текст")]
    [InlineData("Mətn {amount}", "Text", "Текст")]
    [InlineData("", "Text", "Текст")]
    public async Task UnsafeOrIncompatibleTranslationsAreRejected(string az, string en, string ru)
    {
        await using var f = await Fixture.CreateAsync();
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.SaveDraftAsync(Request(0, az, en, ru), default));
        Assert.Equal(0, (await f.Db.TranslationCatalogs.SingleAsync()).Revision);
    }

    [Fact]
    public async Task EditorCannotInventUnknownKeysOrAddNewTemplateParameters()
    {
        await using var f = await Fixture.CreateAsync();
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.SaveDraftAsync(new()
        { ExpectedRevision = 0, Entries = [new("unknown.key", "AZ", "EN", "RU")] }, default));
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.SaveDraftAsync(Request(0, "AZ {new}", "EN {new}", "RU {new}"), default));
    }

    [Fact]
    public async Task SharedMemoryCacheCoalescesConcurrentReadsAndCanBeInvalidated()
    {
        using var cache = new TranslationMemoryCache();
        var snapshot = new TranslationBootstrap().Snapshot;
        var calls = 0;
        // Paralel soyuq oxunuşlarda yalnız bir yükləmənin başladığını ölçür.
        async Task<TranslationSnapshot> Load(CancellationToken token)
        {
            // Paralel soyuq oxunuşlarda tək yükləyicinin çağırıldığını ölçmək üçün qısa gecikmə yaradır.
            Interlocked.Increment(ref calls); await Task.Delay(20, token); return snapshot;
        }
        var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => cache.GetAsync(Load, default)));
        Assert.Equal(1, calls);
        Assert.All(results, result => Assert.Same(snapshot, result));
        cache.Invalidate();
        await cache.GetAsync(Load, default);
        Assert.Equal(2, calls);
    }

    [Theory]
    [InlineData("az", "Yekun hesab")]
    [InlineData("en", "final bill")]
    [InlineData("ru", "счёт")]
    public void SameErrorCodeUsesRequestDictionaryWithoutChangingItsIdentity(string language, string expected)
    {
        var bootstrap = new TranslationBootstrap();
        var http = new DefaultHttpContext();
        http.Items[TextLocalizer.ContextKey] = bootstrap.Snapshot.ForLanguage(language);
        var localizer = new TextLocalizer(new HttpContextAccessor { HttpContext = http }, bootstrap);
        var provider = new ErrorMessageProvider(localizer);
        var error = new BusinessRuleException(ErrorCode.SessionBillUnpaid);
        Assert.Contains(expected, provider.GetMessage(error));
        Assert.Equal(ErrorCode.SessionBillUnpaid, error.Code);
        Assert.Equal(409, error.StatusCode);
    }

    [Fact]
    public void TemplateParametersAreTextAndAreNotRecursivelyEvaluated()
    {
        var output = TextLocalizer.Format("Hello {{name}}, amount {amount}", new { name = "{amount}", amount = "<script>" });
        Assert.Equal("Hello {amount}, amount <script>", output);
        var matcher = new TranslationSourceMatcher([new("message.key", "Rezervasiya #{reservationId} ləğv edildi.")]);
        var match = matcher.Match("Rezervasiya #53 ləğv edildi.");
        Assert.Equal("message.key", match!.Key);
        Assert.Equal("53", match.Arguments["reservationId"]);
        Assert.Null(matcher.Match("User said: Rezervasiya #53 ləğv edildi."));
    }

    [Fact]
    public async Task PublishedReleaseCannotBeModifiedOrDeleted()
    {
        await using var f = await Fixture.CreateAsync();
        var release = await f.Service.PublishAsync(0, null, default);
        var entity = await f.Db.TranslationReleases.SingleAsync(value => value.Id == release.Id);
        entity.CreatedAt = entity.CreatedAt.AddMinutes(1);
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Db.SaveChangesAsync());
    }

    [Fact]
    public async Task DraftUsesRelationalRowsAndKeepsOneTranslationPerKeywordAndLanguage()
    {
        await using var f = await Fixture.CreateAsync();
        await f.Service.SaveDraftAsync(Request(0, "Bir", "One", "Один"), default);
        var keyword = await f.Db.Set<Keyword>().Include(value => value.Translations)
            .ThenInclude(value => value.Language).SingleAsync(value => value.Key == Key);
        Assert.Equal(3, keyword.Translations.Count);
        Assert.Equal("One", keyword.Translations.Single(value => value.Language.Code == "en").Value);
        await f.Service.SaveDraftAsync(Request(1, "İki", "Two", "Два"), default);
        Assert.Equal(3, keyword.Translations.Count);
        Assert.Equal("Two", keyword.Translations.Single(value => value.Language.Code == "en").Value);
        Assert.Null(f.Db.Model.FindEntityType(typeof(TranslationCatalog))!.FindProperty("DraftJson"));
        Assert.Null(f.Db.Model.FindEntityType(typeof(TranslationRelease))!.FindProperty("MessagesJson"));
        Assert.Equal(new[] { "KeywordId", "LanguageId" }, f.Db.Model.FindEntityType(typeof(KeywordTranslation))!
            .FindPrimaryKey()!.Properties.Select(property => property.Name));
    }

    [Fact]
    public async Task PublishedTranslationRowsCannotBeChangedOrAppendedAfterPublication()
    {
        await using var f = await Fixture.CreateAsync();
        var release = await f.Service.PublishAsync(0, null, default);
        var row = await f.Db.Set<TranslationReleaseEntry>().FirstAsync(value => value.TranslationReleaseId == release.Id);
        row.Value = "Changed history";
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Db.SaveChangesAsync());
        f.Db.Entry(row).State = EntityState.Unchanged;
        f.Db.Set<Keyword>().Add(new Keyword { Key = "test.unpublished.append" });
        await f.Db.SaveChangesAsync();
        var keyword = await f.Db.Set<Keyword>().SingleAsync(value => value.Key == "test.unpublished.append");
        f.Db.Set<TranslationReleaseEntry>().Add(new TranslationReleaseEntry
        { TranslationReleaseId = release.Id, KeywordId = keyword.Id, LanguageId = row.LanguageId, Value = "Appended history" });
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Db.SaveChangesAsync());
    }

    [Fact]
    public async Task EveryBundledEntryHasSafeCompatibleTranslations()
    {
        var bootstrap = new TranslationBootstrap();
        foreach (var entry in bootstrap.Snapshot.Entries) TranslationPolicy.ValidateText(entry.Value);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task InflightOldLoadCannotRepopulateCacheAfterPublish()
    {
        using var cache = new TranslationMemoryCache();
        var old = new TranslationSnapshot("old", new TranslationBootstrap().Snapshot.Entries);
        var newer = new TranslationSnapshot("new", old.Entries);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        // İlk DB oxunuşunu yayımın keş silməsindən əvvəl başladıb sonra tamamlayır.
        async Task<TranslationSnapshot> SlowLoad(CancellationToken _)
        { started.SetResult(); await release.Task; return old; }
        var pending = cache.GetAsync(SlowLoad, default);
        await started.Task;
        cache.Invalidate(); release.SetResult(); await pending;
        Assert.Same(newer, await cache.GetAsync(_ => Task.FromResult(newer), default));
    }

    [Fact]
    public async Task DatabaseTimeoutUsesBundledDictionaryButDoesNotMakeAdminWritesSucceed()
    {
        await using var f = await Fixture.CreateAsync();
        f.Repository.Setup(repository => repository.GetCatalogAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException());
        var published = await f.Service.GetPublishedAsync(default);
        Assert.StartsWith("bootstrap-", published.Version);
        Assert.Equal("ru", published.ForLanguage("ru").Language);
        await Assert.ThrowsAsync<TimeoutException>(() => f.Service.SaveDraftAsync(Request(0), default));
        Assert.Empty(f.Db.TranslationReleases);
    }

    [Fact]
    public async Task NotificationsUseRecipientLanguageAndReaderCanChangeLanguageWithoutChangingArguments()
    {
        await using var f = await Fixture.CreateAsync();
        (await f.Db.Users.SingleAsync(user => user.Id == 2)).PreferredLanguage = "ru";
        await f.Db.SaveChangesAsync();
        var bootstrap = new TranslationBootstrap();
        f.Http.Items[TextLocalizer.ContextKey] = bootstrap.Snapshot.ForLanguage("en");
        var text = new TextLocalizer(Mock.Of<IHttpContextAccessor>(accessor => accessor.HttpContext == f.Http), bootstrap);
        var service = new NotificationTextService(f.Service, text, new UserRepository(f.Db));
        var notification = new Notification { UserId = 2, Title = "[TEST] Depozit ödənildi",
            Message = "Rezervasiya #53: Provayder depozit ödənişini təsdiqlədi." };
        await service.PrepareAsync(notification, default);
        Assert.Equal("ru", notification.Language);
        Assert.Equal("notification.depositPaid.title", notification.TitleKey);
        Assert.Contains("Депозит", notification.Title);
        Assert.StartsWith("[TEST] ", notification.Title);
        Assert.Contains("53", notification.Message);
        notification.ChannelId = (int)NotificationChannel.InApp;
        await new NotificationEmailOutboxWriter(f.Db).EnqueueAsync(notification);
        await f.Db.SaveChangesAsync();
        var queued = JsonSerializer.Deserialize<EmailNotificationOutboxPayload>((await f.Db.OutboxEvents.SingleAsync()).Payload)!;
        Assert.Equal("ru", queued.Language);
        Assert.Equal(notification.Message, queued.Body);
        var response = new NotificationResponse();
        service.LocalizeForReader(notification, response);
        Assert.Equal("[TEST] Deposit paid", response.Title);
        Assert.Contains("53", response.Message);
        var custom = new Notification { UserId = 2, Title = "Coffee & Cake", Message = "My own note" };
        await service.PrepareAsync(custom, default);
        Assert.Null(custom.TitleKey); Assert.Null(custom.MessageKey);
        Assert.Equal("My own note", custom.Message);
    }

    [Theory]
    [InlineData("az", "Yeni bildirişiniz var.")]
    [InlineData("en", "You have a new notification.")]
    [InlineData("ru", "У вас новое уведомление.")]
    public async Task MobilePushIsGenericAndUsesRecipientLanguage(string language, string expectedBody)
    {
        var translations = new Mock<ITranslationService>();
        translations.Setup(service => service.GetPublishedAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new TranslationBootstrap().Snapshot);
        using var handler = new PushHandler();
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://push.example.test/") };
        var transport = new ExpoPushTransport(client, new ConfigurationBuilder().Build(), translations.Object);
        var result = await transport.SendLocalizedAsync("token", Guid.NewGuid(), language, default);
        Assert.True(result.Success);
        using var payload = JsonDocument.Parse(handler.Body!);
        Assert.Equal(expectedBody, payload.RootElement.GetProperty("body").GetString());
        Assert.False(payload.RootElement.GetProperty("data").TryGetProperty("reservationId", out _));
        Assert.False(payload.RootElement.GetProperty("data").TryGetProperty("message", out _));
    }

    private sealed class PushHandler : HttpMessageHandler
    {
        public string? Body { get; private set; }
        /// <summary>Real şəbəkəyə çıxmadan göndərilən payload-u saxlayır və uğurlu Expo ticket cavabı qaytarır.</summary>
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new(HttpStatusCode.OK) { Content = new StringContent("{\"data\":{\"status\":\"ok\",\"id\":\"ticket\"}}") };
        }
    }

    [Fact]
    public async Task ResponseLocalizationPreservesNamesBusinessIdsAndWorkflowCommands()
    {
        var bootstrap = new TranslationBootstrap();
        var http = new DefaultHttpContext();
        http.Items[TextLocalizer.ContextKey] = bootstrap.Snapshot.ForLanguage("en");
        var text = new TextLocalizer(Mock.Of<IHttpContextAccessor>(accessor => accessor.HttpContext == http), bootstrap);
        var action = new ActionContext(http, new RouteData(), new ActionDescriptor());
        var reservation = new ReservationResponse { Id = 53, StatusId = 100, Status = "Rezervasiya təsdiqlənib", CustomerName = "Müştəri", RestaurantName = "Sifariş hazırdır", TableName = "Masa-1" };
        var page = new ECafe.Shared.DTOs.PaginatedList<ReservationResponse>([reservation], 1, 1, 20);
        var result = new OkObjectResult(page);
        var executing = new ResultExecutingContext(action, [], result, new object());
        await new LocalizedResponseFilter(text).OnResultExecutionAsync(executing, () => Task.FromResult(new ResultExecutedContext(action, [], result, new object())));
        Assert.Equal("Rezervasiya təsdiqlənib", reservation.Status);
        Assert.Equal("Müştəri", reservation.CustomerName); Assert.Equal("Sifariş hazırdır", reservation.RestaurantName);
        Assert.Equal(100, reservation.StatusId);
        var workflow = new WorkflowActionResponse { Code = "cancel", Endpoint = "/reservations/53", HttpMethod = "POST", Label = "Ləğv et" };
        var workflowResult = new OkObjectResult(new[] { workflow }.Select(value => value));
        await new LocalizedResponseFilter(text).OnResultExecutionAsync(new ResultExecutingContext(action, [], workflowResult, new object()),
            () => Task.FromResult(new ResultExecutedContext(action, [], workflowResult, new object())));
        Assert.Equal("Cancel", workflow.Label); Assert.Equal("cancel", workflow.Code); Assert.Equal("POST", workflow.HttpMethod);
    }

    [Theory]
    [InlineData("az")]
    [InlineData("en")]
    [InlineData("ru")]
    public async Task PublishedDictionarySupportsPerLanguageEtagAnd304(string language)
    {
        var bootstrap = new TranslationBootstrap();
        var mediator = new Mock<IMediator>();
        mediator.Setup(value => value.Send(It.IsAny<TranslationDictionaryQuery>(), It.IsAny<CancellationToken>())).ReturnsAsync(bootstrap.Snapshot);
        using var services = new ServiceCollection().AddSingleton(mediator.Object).BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = services };
        var controller = new LocalizationController { ControllerContext = new ControllerContext { HttpContext = http } };
        var first = Assert.IsType<OkObjectResult>(await controller.GetDictionary(language, default));
        Assert.Equal(language, Assert.IsType<TranslationDictionaryResponse>(first.Value).Language);
        var etag = http.Response.Headers.ETag.ToString();
        http.Request.Headers.IfNoneMatch = etag;
        Assert.Equal(304, Assert.IsType<StatusCodeResult>(await controller.GetDictionary(language, default)).StatusCode);
        http.Request.Headers.IfNoneMatch = "\"other-version\"";
        Assert.IsType<OkObjectResult>(await controller.GetDictionary(language, default));
    }

    /// <summary>Yalnız məlum, parametrsiz xəta açarı ilə təhlükəsiz test qaralaması qurur.</summary>
    private static SaveTranslationDraftRequest Request(long revision, string az = "AZ mətn", string en = "EN text", string ru = "RU текст")
        => new() { ExpectedRevision = revision, Entries = [new(Key, az, en, ru)] };

    private sealed class Fixture : IAsyncDisposable
    {
        public ECafeDbContext Db { get; }
        public DefaultHttpContext Http { get; } = new();
        public TranslationManager Service { get; }
        public TranslationMemoryCache Cache { get; } = new();
        public Func<Task>? BeforeLock { get; set; }
        public Mock<ITranslationRepository> Repository { get; } = new();
        /// <summary>Həqiqi EF repository-sini saxlayır; yalnız real PostgreSQL kilidi və transaction sərhədi test əvəzedicisi ilə qurulur.</summary>
        private Fixture(ECafeDbContext db)
        {
            Db = db; Actor(1, RoleCode.SuperAdmin);
            var repository = new TranslationRepository(db);
            var proxy = Repository;
            proxy.Setup(r => r.GetCatalogAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>())).Returns((bool tracked, CancellationToken token) => repository.GetCatalogAsync(tracked, token));
            proxy.Setup(r => r.GetReleaseAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).Returns((int id, CancellationToken token) => repository.GetReleaseAsync(id, token));
            proxy.Setup(r => r.GetReleasesAsync(It.IsAny<CancellationToken>())).Returns((CancellationToken token) => repository.GetReleasesAsync(token));
            proxy.Setup(r => r.GetDraftEntriesAsync(It.IsAny<CancellationToken>())).Returns((CancellationToken token) => repository.GetDraftEntriesAsync(token));
            proxy.Setup(r => r.GetReleaseEntriesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).Returns((int id, CancellationToken token) => repository.GetReleaseEntriesAsync(id, token));
            proxy.Setup(r => r.SaveDraftEntriesAsync(It.IsAny<IReadOnlyDictionary<string, TranslationText>>(), It.IsAny<CancellationToken>()))
                .Returns((IReadOnlyDictionary<string, TranslationText> entries, CancellationToken token) => repository.SaveDraftEntriesAsync(entries, token));
            proxy.Setup(r => r.AddReleaseAsync(It.IsAny<TranslationRelease>(), It.IsAny<IReadOnlyDictionary<string, TranslationText>>(), It.IsAny<CancellationToken>()))
                .Returns((TranslationRelease release, IReadOnlyDictionary<string, TranslationText> entries, CancellationToken token) => repository.AddReleaseAsync(release, entries, token));
            proxy.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns((CancellationToken token) => repository.SaveChangesAsync(token));
            proxy.Setup(r => r.AcquireLockAsync(It.IsAny<CancellationToken>())).Returns(async () => { if (BeforeLock != null) await BeforeLock(); });
            var transaction = new Mock<IApplicationDbTransaction>();
            transaction.Setup(t => t.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            transaction.Setup(t => t.DisposeAsync()).Returns(ValueTask.CompletedTask);
            var transactions = new Mock<IApplicationDbTransactionFactory>();
            transactions.Setup(t => t.BeginTransactionAsync(IsolationLevel.ReadCommitted, It.IsAny<CancellationToken>())).ReturnsAsync(transaction.Object);
            Service = new(Mock.Of<IHttpContextAccessor>(accessor => accessor.HttpContext == Http), Mock.Of<IMapper>(), new ConfigurationBuilder().Build(),
                proxy.Object, new UserRepository(db), transactions.Object, Cache, new TranslationBootstrap(), TimeProvider.System, Mock.Of<ILogger<TranslationManager>>());
        }
        /// <summary>Aktiv administrator və müştəri ilə ayrıca yaddaş bazası hazırlayır; migration davranışını simulyasiya edən EnsureCreated istifadə edilir.</summary>
        public static async Task<Fixture> CreateAsync()
        {
            var db = new ECafeDbContext(new DbContextOptionsBuilder<ECafeDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
            await db.Database.EnsureCreatedAsync();
            for (var id = 1; id <= 2; id++) db.Users.Add(new User { Id = id, Name = "Test", Surname = "User", Email = $"{id}@example.test", Phone = id.ToString(), Password = "test", IsActive = true, RoleId = id == 1 ? 1 : 5 });
            await db.SaveChangesAsync(); return new(db);
        }
        /// <summary>Token rolunu DB rolundan müstəqil dəyişərək köhnə və uyğun olmayan səlahiyyətləri yoxlayır.</summary>
        public void Actor(int id, RoleCode role) => Http.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("userId", id.ToString()), new Claim(ClaimTypes.Role, ((int)role).ToString())], "test"));
        /// <summary>Hər testin ayrılmış keş və DB resurslarını bağlayır.</summary>
        public async ValueTask DisposeAsync() { Cache.Dispose(); await Db.DisposeAsync(); }
    }
}
