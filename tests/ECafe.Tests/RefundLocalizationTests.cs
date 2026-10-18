using ECafe.Api.Filters;
using ECafe.Application.DTOs.Reservation;
using ECafe.Application.Mappings;
using ECafe.Application.Services.FileAccess.Abstract;
using ECafe.Application.Services.Localization;
using ECafe.Application.Services.Localization.Concrete;
using ECafe.Application.Services.Payments;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Domain.Policies;
using ECafe.Shared.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Moq;
using Xunit;

namespace ECafe.Tests;

public sealed class RefundLocalizationTests
{
    [Theory]
    [InlineData("az", "Geri ödəniş uğursuz oldu", "Geri ödəniş emal edilir", "Provayder geri ödənişi tamamlamadı.")]
    [InlineData("en", "Refund failed", "Refund processing", "The provider did not complete the refund.")]
    [InlineData("ru", "Возврат не выполнен", "Возврат обрабатывается", "Провайдер не завершил возврат.")]
    public async Task FailedRefundAndGeneratedHistoryUseRequestLanguageWithoutChangingStoredData(
        string language, string failedLabel, string processingLabel, string failedReason)
    {
        var entity = CreateRefund(RefundStatus.Failed);
        entity.StatusHistory = [
            new() { Id = 1, ToStatusId = StatusIds.Refund(RefundStatus.Processing), ChangedByUserId = 4,
                Reason = PaymentTexts.RefundPending, ChangedAt = entity.RequestedAt },
            new() { Id = 2, FromStatusId = StatusIds.Refund(RefundStatus.Processing),
                ToStatusId = entity.StatusId, Reason = ProviderRefundPolicy.FailedReason,
                ChangedAt = entity.RequestedAt.AddMinutes(1) }
        ];
        var response = ReservationRefundResponseMapper.Map(entity, Mock.Of<IFileAccessUrlService>());
        var originalNextStep = response.CustomerNextStep;
        var originalEligibility = response.EligibilityReason;
        var text = await LocalizeAsync(response, language);

        Assert.Equal(failedLabel, response.StatusLabel);
        Assert.Equal(RefundStatus.Failed.GetName(), response.Status);
        Assert.Equal(entity.StatusId, response.StatusId);
        Assert.Equal(text.TranslateLiteral(originalNextStep!), response.CustomerNextStep);
        Assert.Equal(text.TranslateLiteral(originalEligibility), response.EligibilityReason);
        Assert.Equal(processingLabel, response.History[0].ToStatusLabel);
        Assert.Equal(text.TranslateLiteral(PaymentTexts.RefundPending), response.History[0].ReasonLabel);
        Assert.Equal("Customer", response.History[0].ActorType);
        Assert.Equal(failedReason, response.History[1].ReasonLabel);
        Assert.Equal(processingLabel, response.History[1].FromStatusLabel);
        Assert.Equal("System", response.History[1].ActorType);
        Assert.Equal(8004, response.History[1].FromStatusId);
        Assert.Equal(8007, response.History[1].ToStatusId);
        Assert.Equal(ProviderRefundPolicy.FailedReason, response.History[1].Reason);
        Assert.Equal(PaymentTexts.RefundPending, entity.StatusHistory.First().Reason);
        Assert.Equal(originalEligibility, entity.EligibilityReason);
        Assert.Equal(entity.CancellationReasonSnapshot, response.CancellationReason);
        Assert.Equal(20m, response.Amount);
        Assert.Equal("AZN", response.CurrencyCode);
        Assert.True(response.IsTest);
        Assert.False(response.CanSimulate);
    }

    [Theory]
    [InlineData("az")]
    [InlineData("en")]
    [InlineData("ru")]
    public async Task AllRefundStagesHaveLocalizedDisplayLabelsAndNextSteps(string language)
    {
        foreach (var status in Enum.GetValues<RefundStatus>())
        {
            var entity = CreateRefund(status);
            var response = ReservationRefundResponseMapper.Map(entity, Mock.Of<IFileAccessUrlService>());
            var sourceNextStep = response.CustomerNextStep;
            var text = await LocalizeAsync(response, language);
            Assert.Equal(status.GetName(), response.Status);
            Assert.Equal(entity.StatusId, response.StatusId);
            Assert.Equal(text.TranslateLiteral(status.GetName()), response.StatusLabel);
            Assert.Equal(text.TranslateLiteral(sourceNextStep!), response.CustomerNextStep);
            if (language != "az")
            {
                Assert.NotEqual(response.Status, response.StatusLabel);
                Assert.NotEqual(sourceNextStep, response.CustomerNextStep);
            }
        }
    }

    [Theory]
    [InlineData("en")]
    [InlineData("ru")]
    public async Task FreeTextCancellationAndHistoryAreNotTranslatedEvenWhenTheyMatchSystemText(string language)
    {
        var entity = CreateRefund(RefundStatus.Disputed);
        entity.CancellationReasonSnapshot = "Geri ödəniş uğursuz oldu";
        entity.StatusHistory = [new() { Id = 3, ToStatusId = StatusIds.Refund(RefundStatus.Disputed),
            ChangedByUserId = 4, Reason = ProviderRefundPolicy.FailedReason }];
        var response = ReservationRefundResponseMapper.Map(entity, Mock.Of<IFileAccessUrlService>());
        await LocalizeAsync(response, language);
        Assert.Equal(entity.CancellationReasonSnapshot, response.CancellationReason);
        Assert.Equal(ProviderRefundPolicy.FailedReason, response.History[0].Reason);
        Assert.Null(response.History[0].ReasonLabel);
    }

    [Theory]
    [InlineData("en")]
    [InlineData("ru")]
    public async Task CompletedAndLegacyManualRefundsKeepTheirBusinessFlags(string language)
    {
        var entity = CreateRefund(RefundStatus.Refunded);
        entity.RefundedAt = entity.RequestedAt.AddMinutes(2);
        entity.StatusHistory = [new() { Id = 4, ToStatusId = entity.StatusId,
            Reason = ProviderRefundPolicy.SucceededReason }];
        var response = ReservationRefundResponseMapper.Map(entity, Mock.Of<IFileAccessUrlService>());
        var text = await LocalizeAsync(response, language);
        Assert.Equal(text.TranslateLiteral(ProviderRefundPolicy.SucceededReason), response.History[0].ReasonLabel);
        Assert.Equal(entity.RefundedAt, response.RefundedAt!.Value.UtcDateTime);
        Assert.False(response.CanSimulate);
        Assert.False(response.RequiresManualReview);

        entity.StatusId = StatusIds.Refund(RefundStatus.AwaitingPayoutDetails);
        entity.SourcePaymentId = null;
        entity.EligibilityReason = PaymentTexts.ManualReview;
        entity.StatusHistory.Clear();
        var manual = ReservationRefundResponseMapper.Map(entity, Mock.Of<IFileAccessUrlService>());
        await LocalizeAsync(manual, language);
        Assert.True(manual.RequiresManualReview);
        Assert.Equal(text.TranslateLiteral(PaymentTexts.ManualReview), manual.CustomerNextStep);
        Assert.Equal(text.TranslateLiteral(PaymentTexts.ManualReview), manual.EligibilityReason);
    }

    [Theory]
    [InlineData("az")]
    [InlineData("en")]
    [InlineData("ru")]
    public async Task RefundRequestPromptPreservesTheAmountInEveryLanguage(string language)
    {
        var reservation = RefundRequestInformationTests.CreateReservation();
        var prompt = ReservationRefundRequestMapper.Map(reservation)!;
        var original = prompt.Message;
        var text = await LocalizeAsync(prompt, language);
        Assert.Equal(text.TranslateLiteral(original), prompt.Message);
        Assert.Contains("20.00 AZN", prompt.Message);
        Assert.Equal(20m, prompt.Amount);
        if (language != "az") Assert.NotEqual(original, prompt.Message);
    }

    /// <summary>Real HTTP sorğusundakı dil snapshot-ını və cavab filtrini DB-yə və provayderə yazmadan işlədərək görünüşü yoxlayır.</summary>
    private static async Task<TextLocalizer> LocalizeAsync(object response, string language)
    {
        var bootstrap = new TranslationBootstrap();
        var http = new DefaultHttpContext();
        http.Items[TextLocalizer.ContextKey] = bootstrap.Snapshot.ForLanguage(language);
        var text = new TextLocalizer(Mock.Of<IHttpContextAccessor>(accessor => accessor.HttpContext == http), bootstrap);
        var context = new ActionContext(http, new RouteData(), new ActionDescriptor());
        var result = new OkObjectResult(response);
        await new LocalizedResponseFilter(text).OnResultExecutionAsync(
            new ResultExecutingContext(context, [], result, new object()),
            () => Task.FromResult(new ResultExecutedContext(context, [], result, new object())));
        return text;
    }

    /// <summary>İlkin ödənişlə bağlı test refundu qurur; maliyyə sahələrinin yalnız tərcümədən dəyişmədiyini ölçməyə imkan verir.</summary>
    private static ReservationRefund CreateRefund(RefundStatus status) => new()
    {
        Id = 9, ReservationId = 53, SourcePaymentId = 10, SourcePayment = new Payment { Id = 10, IsTest = true },
        Reservation = new Reservation { Id = 53, CustomerUserId = 4 },
        StatusId = StatusIds.Refund(status), Amount = 20m, CurrencyCode = "AZN",
        RequestedAt = new DateTime(2026, 10, 18, 2, 15, 0, DateTimeKind.Utc),
        EligibilityReason = "Restoran təsdiqlənmiş rezervasiyanı ləğv edib.",
        CancellationReasonSnapshot = "Müştərinin öz qeydi"
    };
}
