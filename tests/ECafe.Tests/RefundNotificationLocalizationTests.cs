using System.Text.Json;
using ECafe.Application.DTOs.Notification;
using ECafe.Application.Repositories.User;
using ECafe.Application.Services.Localization;
using ECafe.Application.Services.Localization.Abstract;
using ECafe.Application.Services.Localization.Concrete;
using ECafe.Application.Services.Payments;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Infrastructure.Context;
using ECafe.Infrastructure.Repositories.User;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ECafe.Tests;

public sealed class RefundNotificationLocalizationTests
{
    private static readonly TranslationBootstrap Bootstrap = new();

    [Theory]
    [InlineData("az", "Geri ödəniş yeniləndi", "Geri ödəniş sorğusu")]
    [InlineData("en", "Refund updated", "Refund request")]
    [InlineData("ru", "Статус возврата обновлён", "Запрос возврата")]
    public async Task NewRefundNotificationsUseRecipientLanguageAndKeepTestLabels(string language, string updated, string requested)
    {
        await using var db = new ECafeDbContext(new DbContextOptionsBuilder<ECafeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Users.Add(new User { Id = 42, Name = "Test", Surname = "Recipient", Email = "recipient@example.test",
            Phone = "+994500000000", Password = "unused-test-hash", IsActive = true,
            RoleId = (int)RoleCode.Customer, PreferredLanguage = language });
        await db.SaveChangesAsync();
        var translations = new Mock<ITranslationService>();
        translations.Setup(service => service.GetPublishedAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Bootstrap.Snapshot);
        var service = new NotificationTextService(translations.Object, Reader("en"), new UserRepository(db));

        foreach (var (title, key, expected, message, english) in new[] {
            ("Geri ödəniş yeniləndi", "notification.refundUpdated.title", updated, PaymentTexts.RefundCompleted, "Refund updated"),
            ("Geri ödəniş sorğusu", "notification.providerRefundRequested.title", requested, PaymentTexts.RefundPending, "Refund request") })
        foreach (var prefix in new[] { "", "[TEST] " })
        {
            var notification = new Notification { UserId = 42, Title = prefix + title,
                Message = prefix.Length == 0 ? message : PaymentTexts.TestNotice + " " + message };
            await service.PrepareAsync(notification, default);
            Assert.Equal(language, notification.Language);
            Assert.Equal(key, notification.TitleKey);
            Assert.NotNull(notification.MessageKey);
            Assert.Equal(prefix + expected, notification.Title);
            if (language != "az") Assert.DoesNotContain(message, notification.Message);
            var storedTitle = notification.Title;
            var storedMessage = notification.Message;
            var response = new NotificationResponse();
            service.LocalizeForReader(notification, response);
            Assert.Equal(prefix + english, response.Title);
            Assert.Equal(storedTitle, notification.Title);
            Assert.Equal(storedMessage, notification.Message);
            Assert.Equal(language, notification.Language);
        }
    }

    [Theory]
    [InlineData("az", "Geri ödəniş yeniləndi", "Geri ödəniş sorğusu")]
    [InlineData("en", "Refund updated", "Refund request")]
    [InlineData("ru", "Статус возврата обновлён", "Запрос возврата")]
    public void ExistingPartialRefundNotificationsGainMissingTitlesWithoutDatabaseChanges(string language, string updated, string requested)
    {
        var service = new NotificationTextService(Mock.Of<ITranslationService>(), Reader(language), Mock.Of<IUserRepository>());
        foreach (var (title, expected) in new[] { ("Geri ödəniş yeniləndi", updated), ("Geri ödəniş sorğusu", requested) })
        foreach (var prefix in new[] { "", "[TEST] " })
        {
            var arguments = JsonSerializer.Serialize(new NotificationTextService.Arguments(null,
                prefix.Length == 0 ? new Dictionary<string, object?>() : new Dictionary<string, object?> { ["__prefix"] = "notice" }));
            var notification = new Notification { Title = prefix + title, Message = "Stored body",
                MessageKey = "api.literal.refundComplete", TranslationArgumentsJson = arguments, Language = "ru" };
            var response = Copy(notification);
            service.LocalizeForReader(notification, response);
            Assert.Equal(prefix + expected, response.Title);
            Assert.Contains(language == "ru" ? "возврат" : language == "en" ? "refund" : "geri ödənişi", response.Message);
            Assert.Null(notification.TitleKey);
            Assert.Equal(prefix + title, notification.Title);
            Assert.Equal("Stored body", notification.Message);
            Assert.Equal(arguments, notification.TranslationArgumentsJson);
            Assert.Equal("ru", notification.Language);
        }
    }

    [Theory]
    [InlineData("az", "Müştəri rezervasiya #117 üçün ləğv sorğusu göndərdi.")]
    [InlineData("en", "The customer requested cancellation of reservation #117.")]
    [InlineData("ru", "Клиент отправил запрос на отмену бронирования № 117.")]
    public void ExistingCancellationNotificationKeepsReservationNumberWhenMissingBodyIsLocalized(string language, string expected)
    {
        var notification = new Notification { Title = "Бронирование отменено", TitleKey = "notification.reservationCancelled.title",
            Message = "Müştəri rezervasiya #117 üçün ləğv sorğusu göndərdi.",
            TranslationArgumentsJson = JsonSerializer.Serialize(new NotificationTextService.Arguments(new Dictionary<string, object?>(), null)) };
        var response = Copy(notification);
        var service = new NotificationTextService(Mock.Of<ITranslationService>(), Reader(language), Mock.Of<IUserRepository>());
        service.LocalizeForReader(notification, response);
        Assert.Equal(expected, response.Message);
        Assert.Equal("117", Reader(language).ResolveNotificationSource(notification.Message)!.Arguments["reservationId"]);
        Assert.Null(notification.MessageKey);
        Assert.Equal("Müştəri rezervasiya #117 üçün ləğv sorğusu göndərdi.", notification.Message);
    }

    [Fact]
    public void FreeTextAndFullyUnkeyedHistoricalNotificationsAreNotReinterpreted()
    {
        var service = new NotificationTextService(Mock.Of<ITranslationService>(), Reader("ru"), Mock.Of<IUserRepository>());
        foreach (var json in new[] { null, "{\"Title\":null,\"Message\":null}" })
        {
            var notification = new Notification { Title = "Geri ödəniş yeniləndi", Message = "My own note",
                TranslationArgumentsJson = json };
            var response = Copy(notification);
            service.LocalizeForReader(notification, response);
            Assert.Equal(notification.Title, response.Title);
            Assert.Equal(notification.Message, response.Message);
        }
        var partial = new Notification { Title = "Rezervasiya ləğv edildi", TitleKey = "notification.reservationCancelled.title",
            Message = "Coffee & Cake #117", TranslationArgumentsJson = "{\"Title\":{},\"Message\":null}" };
        var custom = Copy(partial);
        service.LocalizeForReader(partial, custom);
        Assert.Equal("Coffee & Cake #117", custom.Message);
    }

    /// <summary>Oxuyanın cari dilini HTTP lüğətində seçir; DB-yə və istifadəçi seçimlərinə yazmır.</summary>
    private static TextLocalizer Reader(string language)
    {
        var http = new DefaultHttpContext();
        http.Items[TextLocalizer.ContextKey] = Bootstrap.Snapshot.ForLanguage(language);
        return new TextLocalizer(new HttpContextAccessor { HttpContext = http }, Bootstrap);
    }

    /// <summary>Repository mapping-i kimi saxlanmış mətni cavaba köçürür ki, oxuma zamanı entity-nin dəyişmədiyi yoxlanılsın.</summary>
    private static NotificationResponse Copy(Notification notification) => new() { Title = notification.Title, Message = notification.Message };
}
