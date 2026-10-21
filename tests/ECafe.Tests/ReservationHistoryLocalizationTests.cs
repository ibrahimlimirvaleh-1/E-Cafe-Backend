using ECafe.Api.Filters;
using ECafe.Application.Mappings;
using ECafe.Application.Services.Localization;
using ECafe.Application.Services.Localization.Concrete;
using ECafe.Domain.Constants;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Moq;
using Xunit;

namespace ECafe.Tests;

public sealed class ReservationHistoryLocalizationTests
{
    [Theory]
    [InlineData("az", "Depozitsiz rezervasiya avtomatik təsdiqləndi.")]
    [InlineData("en", "The reservation was automatically confirmed without a deposit.")]
    [InlineData("ru", "Бронирование автоматически подтверждено без депозита.")]
    public async Task InitialConfirmationHasLocalizedLabelWithoutChangingStoredHistory(string language, string expected)
    {
        var reservation = CreateReservation();
        var response = ReservationHistoryResponseMapper.Map(reservation);
        await LocalizeAsync(response, language);
        var item = Assert.Single(response.Items);
        Assert.Equal(expected, item.ReasonLabel);
        Assert.Equal(ReservationHistoryTexts.NoDepositConfirmed, item.Reason);
        Assert.Equal(item.Reason, reservation.StatusHistory.Single().Reason);
        Assert.Equal("Customer", item.ActorType);
        Assert.Equal(TimeSpan.Zero, item.ChangedAt.Offset);
        Assert.Equal(reservation.ArrivedAt, response.ArrivedAt?.UtcDateTime);
        Assert.Equal(reservation.SeatedAt, response.SeatedAt?.UtcDateTime);
    }

    [Theory]
    [InlineData(ReservationStatus.PendingPayment)]
    [InlineData(ReservationStatus.AwaitingPaymentInstruction)]
    public async Task DepositReservationCreationIsLocalized(ReservationStatus status)
    {
        var reservation = CreateReservation();
        var history = reservation.StatusHistory.Single();
        history.ToStatusId = StatusIds.Reservation(status);
        history.Reason = ReservationHistoryTexts.Created;
        var response = ReservationHistoryResponseMapper.Map(reservation);
        await LocalizeAsync(response, "en");
        Assert.Equal("The reservation was created.", Assert.Single(response.Items).ReasonLabel);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task UnrelatedOrFreeTextHistoryDoesNotAcquireTranslatedLabel(int variant)
    {
        var reservation = CreateReservation();
        var history = reservation.StatusHistory.Single();
        if (variant == 1) history.FromStatusId = StatusIds.Reservation(ReservationStatus.PendingPayment);
        if (variant == 2) history.ChangedByUserId = 99;
        if (variant == 3) history.ToStatusId = StatusIds.Reservation(ReservationStatus.Cancelled);
        if (variant == 4) history.Reason = "Müştərinin öz qeydi";
        var original = history.Reason;
        var response = ReservationHistoryResponseMapper.Map(reservation);
        await LocalizeAsync(response, "ru");
        Assert.Null(Assert.Single(response.Items).ReasonLabel);
        Assert.Equal(original, response.Items[0].Reason);
        Assert.Equal(original, history.Reason);
    }

    /// <summary>İlkin avtomatik təsdiqin real status və müəllif məlumatını bazaya yazmadan qurur.</summary>
    private static Reservation CreateReservation() => new()
    {
        Id = 80, CustomerUserId = 4,
        ArrivedAt = new DateTime(2026, 10, 21, 9, 0, 0, DateTimeKind.Utc),
        SeatedAt = new DateTime(2026, 10, 21, 9, 5, 0, DateTimeKind.Utc),
        StatusHistory = [new()
        {
            Id = 1, ToStatusId = StatusIds.Reservation(ReservationStatus.Confirmed),
            ChangedByUserId = 4, ChangedAt = new DateTime(2026, 10, 21, 8, 0, 0, DateTimeKind.Utc),
            Reason = ReservationHistoryTexts.NoDepositConfirmed
        }]
    };

    /// <summary>Yayımlanmış sorğu dili snapshot-ını filtrə verir; yalnız görünüş sahəsinin tərcümə olunduğunu yoxlayır.</summary>
    private static async Task LocalizeAsync(object response, string language)
    {
        var bootstrap = new TranslationBootstrap();
        var http = new DefaultHttpContext();
        http.Items[TextLocalizer.ContextKey] = bootstrap.Snapshot.ForLanguage(language);
        var text = new TextLocalizer(Mock.Of<IHttpContextAccessor>(a => a.HttpContext == http), bootstrap);
        var action = new ActionContext(http, new RouteData(), new ActionDescriptor());
        var result = new OkObjectResult(response);
        await new LocalizedResponseFilter(text).OnResultExecutionAsync(
            new ResultExecutingContext(action, [], result, new object()),
            () => Task.FromResult(new ResultExecutedContext(action, [], result, new object())));
    }
}
