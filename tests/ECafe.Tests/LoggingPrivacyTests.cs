using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ECafe.Api.Middlewares;
using ECafe.Api.Monitoring;
using ECafe.Application.Common.Errors;
using ECafe.Application.Common.Outbox;
using ECafe.Application.Services;
using ECafe.Application.Services.Monitoring.Abstract;
using ECafe.Application.Services.Outbox.Concrete;
using ECafe.Domain.Entities;
using ECafe.Infrastructure.Context;
using ECafe.Infrastructure.Repositories;
using ECafe.Shared.Diagnostics;
using ECafe.Application.Common.Exceptions;
using ECafe.Application.DTOs.Auth;
using ECafe.Application.Repositories.UserRefreshToken;
using ECafe.Application.Repository;
using ECafe.Application.Services.Auth.Concrete;
using ECafe.Application.Services.Auth.Abstract;
using ECafe.Application.Services.Localization;
using ECafe.Infrastructure.Repositories.User;
using ECafe.Infrastructure.Repositories.PasswordResetToken;
using ECafe.Infrastructure.Repositories.UserPasswordSetupToken;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Sentry;
using Xunit;

namespace ECafe.Tests;

public sealed class LoggingPrivacyTests
{
    private const string Secret = "SYNTHETIC_PRIVATE_VALUE_DO_NOT_SEND";

    [Theory]
    [InlineData("invalid")]
    [InlineData("resetComplete")]
    [InlineData("setComplete")]
    [InlineData("resetFailed")]
    [InlineData("setFailed")]
    public void PasswordLinkMessagesExistInEveryBackendLanguage(string name)
    {
        var snapshot = new TranslationBootstrap().Snapshot;
        foreach (var language in new[] { "az", "en", "ru" })
            Assert.False(string.IsNullOrWhiteSpace(snapshot.ForLanguage(language).Messages["ui.passwordLink." + name]));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GeneratedPasswordEmailsUseFragmentAndNeverQueryForTokens(bool setup)
    {
        await using var db = new ECafeDbContext(new DbContextOptionsBuilder<ECafeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var user = new User { Id = 71, Name = "Test", Surname = "Only", Email = "test@example.test", Phone = "71", Password = "synthetic-not-used", IsActive = true, RoleId = 5 };
        db.Roles.Add(new Role { Id = 5, Name = "Customer" });
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["Frontend:BaseUrl"] = "https://app.example.test" }).Build();
        var email = new Mock<IEmailOutboxService>();
        var refresh = new Mock<IUserRefreshTokenRepository>();
        var factory = new Mock<IApplicationDbTransactionFactory>();
        factory.Setup(value => value.BeginTransactionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Mock.Of<IApplicationDbTransaction>());
        if (setup)
            await new PasswordSetupManager(new UserRepository(db), new UserPasswordSetupTokenRepository(db), email.Object,
                config, refresh.Object, factory.Object).SendSetupLinkAsync(user);
        else
            await new PasswordResetManager(new UserRepository(db), new PasswordResetTokenRepository(db), refresh.Object, email.Object,
                new HttpContextAccessor(), config, Mock.Of<ILogger<PasswordResetManager>>(), Mock.Of<IUserSessionStateCache>(), factory.Object)
                .RequestPasswordResetAsync(new ForgotPasswordRequest { Email = user.Email });
        var body = Assert.IsType<string>(Assert.Single(email.Invocations).Arguments[3]);
        Assert.Contains(setup ? "/set-password#token=" : "/reset-password#token=", body);
        Assert.DoesNotContain("?token=", body);
    }

    [Fact]
    public void DiagnosticExceptionDropsTextInnerDataAndCustomStackTrace()
    {
        var error = new HostileException(Secret, new Exception(Secret));
        error.Data["password"] = Secret;
        var safe = SafeDiagnosticException.ForLogging(error);
        Assert.Equal(nameof(HostileException), safe.Message);
        Assert.Null(safe.InnerException);
        Assert.Empty(safe.Data);
        Assert.DoesNotContain(Secret, safe.ToString());
        Assert.Same(safe, SafeDiagnosticException.ForLogging(safe));
        Assert.Equal(nameof(HostileException), SafeDiagnosticException.Code(error));
        Assert.Equal(Secret, error.Message);
        var input = EventJson();
        input["exception"]!["values"]![0]!["type"] = typeof(SafeDiagnosticException).FullName;
        input["exception"]!["values"]![0]!["value"] = nameof(HostileException);
        var filtered = SentryPrivacy.FilterEvent(SentryEvent.FromJson(JsonSerializer.SerializeToElement(input)))!;
        var payload = Snapshot(writer => filtered.WriteTo(writer, null));
        Assert.Contains(nameof(HostileException), payload);
        Assert.DoesNotContain(Secret, payload);
    }

    [Fact]
    public void DiagnosticExceptionRetainsRealCallSiteWithoutSourceFiles()
    {
        var error = Record.Exception(ThrowSynthetic);
        var safe = SafeDiagnosticException.ForLogging(error!);
        Assert.Contains(nameof(ThrowSynthetic), safe.StackTrace);
        Assert.DoesNotContain(Secret, safe.ToString());
    }

    [Fact]
    public void SentryPrivacyCannotBeBypassedByEnablingBodyPiiOrStructuredLogs()
    {
        var options = new Sentry.AspNetCore.SentryAspNetCoreOptions
        { SendDefaultPii = true, EnableLogs = true, MaxRequestBodySize = Sentry.Extensibility.RequestSize.Always };
        SentryPrivacy.Apply(options);
        Assert.False(options.SendDefaultPii);
        Assert.False(options.EnableLogs);
        Assert.Equal(Sentry.Extensibility.RequestSize.None, options.MaxRequestBodySize);
        Assert.True(SentryPrivacy.SuppressMiddlewareLogger(typeof(ExceptionHandlingMiddleware).FullName!, LogLevel.Error, default, new Exception()));
        Assert.False(SentryPrivacy.SuppressMiddlewareLogger("ECafe.Api.BackgroundServices.EmailOutboxWorker", LogLevel.Error, default, new Exception()));
    }

    [Theory]
    [InlineData("password")]
    [InlineData("access_token")]
    [InlineData("refreshToken")]
    [InlineData("Cookie")]
    public void SentryEventKeepsExceptionTypeFramesAndTraceButNotSensitivePayload(string key)
    {
        var input = EventJson();
        input["request"] = new JsonObject
        {
            ["url"] = $"https://user:{Secret}@example.test/{Secret}?{key}={Secret}#{Secret}",
            ["method"] = "POST", ["data"] = new JsonObject { [key] = Secret },
            ["headers"] = new JsonObject { [key] = Secret }, ["cookies"] = Secret, ["query_string"] = $"{key}={Secret}"
        };
        var before = input.ToJsonString();
        var source = SentryEvent.FromJson(JsonSerializer.SerializeToElement(input));
        var filtered = Assert.IsType<SentryEvent>(SentryPrivacy.FilterEvent(source));
        var text = Snapshot(writer => filtered.WriteTo(writer, null));
        Assert.DoesNotContain(Secret, text);
        using var output = JsonDocument.Parse(text);
        Assert.Equal("https://example.test", output.RootElement.GetProperty("request").GetProperty("url").GetString());
        Assert.Equal("POST", output.RootElement.GetProperty("request").GetProperty("method").GetString());
        Assert.Equal("InvalidOperationException", output.RootElement.GetProperty("exception").GetProperty("values")[0].GetProperty("type").GetString());
        Assert.Contains("PaymentService.Confirm", text);
        Assert.Contains("traceId", text);
        Assert.DoesNotContain("breadcrumbs", text);
        Assert.DoesNotContain("abs_path", text);
        Assert.Equal(before, input.ToJsonString());
        Assert.Contains(Secret, Snapshot(writer => source.WriteTo(writer, null)));
    }

    [Fact]
    public void SentryTransactionKeepsSpanTimingsButDropsSqlAndHttpPayload()
    {
        var input = EventJson();
        input.Remove("exception");
        input["type"] = "transaction";
        input["transaction"] = Secret;
        input["timestamp"] = "2023-11-14T22:13:21Z";
        input["start_timestamp"] = "2023-11-14T22:13:20Z";
        input["spans"] = new JsonArray(new JsonObject
        {
            ["trace_id"] = "0123456789abcdef0123456789abcdef", ["span_id"] = "0123456789abcdef", ["op"] = "db",
            ["description"] = Secret, ["data"] = new JsonObject { ["db.query"] = Secret },
            ["start_timestamp"] = "2023-11-14T22:13:20Z", ["timestamp"] = "2023-11-14T22:13:21Z"
        });
        var source = SentryTransaction.FromJson(JsonSerializer.SerializeToElement(input));
        var clean = Assert.IsType<SentryTransaction>(SentryPrivacy.FilterTransaction(source));
        var text = Snapshot(writer => clean.WriteTo(writer, null));
        Assert.DoesNotContain(Secret, text);
        Assert.Contains("ECafe request", text);
        Assert.Contains("0123456789abcdef", text);
        using var document = JsonDocument.Parse(text);
        Assert.Single(document.RootElement.GetProperty("spans").EnumerateArray());
    }

    [Fact]
    public async Task MiddlewareLogsSafeExceptionAndReturnsGenericErrorWithSameTraceId()
    {
        var logger = new CollectingLogger<ExceptionHandlingMiddleware>();
        var middleware = new ExceptionHandlingMiddleware(_ => throw new InvalidOperationException(Secret), logger,
            new ErrorMessageProvider(), Mock.Of<ICriticalEventReporter>());
        var http = new DefaultHttpContext { TraceIdentifier = "safe-trace-id" };
        http.Response.Body = new MemoryStream();
        await middleware.Invoke(http);
        var log = Assert.Single(logger.Messages);
        Assert.Contains("safe-trace-id", log);
        Assert.DoesNotContain(Secret, log);
        http.Response.Body.Position = 0;
        var body = await new StreamReader(http.Response.Body).ReadToEndAsync();
        Assert.Equal(500, http.Response.StatusCode);
        Assert.DoesNotContain(Secret, body);
        Assert.DoesNotContain("InvalidOperationException", body);
        Assert.Contains("safe-trace-id", body);
    }

    [Fact]
    public async Task FailedEmailStoresOnlyErrorTypeAndAdminDoesNotReceiveHistoricalRawErrors()
    {
        await using var db = new ECafeDbContext(new DbContextOptionsBuilder<ECafeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var row = new OutboxEvent
        {
            Id = Guid.NewGuid(), EventType = OutboxEventTypes.EmailNotificationRequested, AggregateType = "Test", AggregateId = 1,
            OccurredAt = DateTime.UtcNow, Payload = JsonSerializer.Serialize(new EmailNotificationOutboxPayload
            { ToEmail = "test@example.test", ToName = "Test", Subject = "Test", Body = "Test" })
        };
        db.OutboxEvents.Add(row);
        await db.SaveChangesAsync();
        var email = new Mock<IEmailService>();
        email.Setup(value => value.SendContractNotificationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new IOException(Secret));
        var repository = new BaseRepository<OutboxEvent>(db);
        var config = new ConfigurationBuilder().Build();
        await new EmailOutboxManager(repository, email.Object, Mock.Of<ICriticalEventReporter>(), config).ProcessPendingAsync(10);
        Assert.Equal(nameof(IOException), row.LastError);
        Assert.Equal(1, row.RetryCount);
        Assert.Null(row.ProcessedAt);
        row.LastError = Secret;
        await db.SaveChangesAsync();
        var response = await new OutboxAdminManager(repository, config).GetMessageAsync(row.Id);
        Assert.Equal(ErrorMessageProvider.DefaultMessages[ECafe.Domain.Exceptions.ErrorCode.NotificationProviderUnavailable], response.LastError);
        Assert.Equal(Secret, row.LastError);
    }

    /// <summary>Yalnız yaddaşda sintetik məxfi mətnli Sentry payload-u qurur; heç bir real istifadəçi və şəbəkə göndərişi yoxdur.</summary>
    private static JsonObject EventJson() => new()
    {
        ["event_id"] = "0123456789abcdef0123456789abcdef", ["timestamp"] = 1700000001d,
        ["level"] = "error", ["platform"] = "csharp", ["message"] = new JsonObject { ["formatted"] = Secret },
        ["extra"] = new JsonObject { ["newSecretField"] = Secret }, ["user"] = new JsonObject { ["email"] = Secret },
        ["tags"] = new JsonObject { ["traceId"] = "safe-trace-id", ["authorization"] = Secret },
        ["contexts"] = new JsonObject { ["trace"] = new JsonObject
            { ["trace_id"] = "0123456789abcdef0123456789abcdef", ["span_id"] = "0123456789abcdef", ["op"] = "http.server", ["data"] = new JsonObject { ["token"] = Secret } },
            ["privateContext"] = new JsonObject { ["value"] = Secret } },
        ["breadcrumbs"] = new JsonArray(new JsonObject { ["message"] = Secret }),
        ["exception"] = new JsonObject { ["values"] = new JsonArray(new JsonObject
        {
            ["type"] = "InvalidOperationException", ["value"] = Secret,
            ["mechanism"] = new JsonObject { ["type"] = "generic", ["handled"] = false, ["data"] = new JsonObject { ["token"] = Secret } },
            ["stacktrace"] = new JsonObject { ["frames"] = new JsonArray(new JsonObject
            { ["function"] = "PaymentService.Confirm", ["lineno"] = 42, ["abs_path"] = Secret,
                ["context_line"] = Secret, ["vars"] = new JsonObject { ["password"] = Secret } }) }
        }) }
    };

    /// <summary>SDK-nın serializasiya nəticəsini yoxlamaq üçün yalnız yaddaş axını istifadə edir.</summary>
    private static string Snapshot(Action<Utf8JsonWriter> write)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream)) write(writer);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>Stack trace-in çağırış yerini saxlamaqla məxfi exception mətninin atılmasını sınaqdan keçirir.</summary>
    private static void ThrowSynthetic() => throw new InvalidOperationException(Secret);

    private sealed class HostileException(string message, Exception inner) : Exception(message, inner)
    { public override string StackTrace => Secret; }

    private sealed class CollectingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];
        /// <summary>Test mesajını exception serializasiyası ilə birlikdə tutur; real sink-ə göndərmir.</summary>
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? error, Func<TState, Exception?, string> formatter)
            => Messages.Add(formatter(state, error) + error);
        /// <summary>Test logger-i bütün səviyyələri müşahidə edir.</summary>
        public bool IsEnabled(LogLevel level) => true;
        /// <summary>Bu sadə sink heç bir scope və ya xarici resurs yaratmır.</summary>
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    }
}
