using System.Reflection;
using System.Text.Json;
using ECafe.Api.Controllers;
using ECafe.Api.Security;
using ECafe.Application.DTOs.Payment;
using ECafe.Application.Mappings;
using ECafe.Application.Services.FileAccess.Abstract;
using ECafe.Application.Services.Payments;
using ECafe.Application.Services.Payments.Concrete;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using ECafe.Domain.Policies;
using ECafe.Domain.Services;
using ECafe.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using StatusType = ECafe.Domain.Enums.StatusType;

namespace ECafe.Tests;

public sealed class ProviderPaymentPolicyTests
{
    private static readonly DateTime Now = new(2026, 10, 18, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task LegacyTransferStatusRemainsReadableButCannotBeUsedForNewPayments()
    {
        var options = new DbContextOptionsBuilder<ECafeDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var context = new ECafeDbContext(options);
        await context.Database.EnsureCreatedAsync();
        var methodId = StatusIds.Build(StatusType.PaymentMethod, (int)PaymentMethod.BankTransfer);
        Assert.True(await context.Set<Status>().AnyAsync(status => status.Id == methodId && !status.IsDeleted));
        Assert.False(PaymentMethodPolicy.IsMvpSupported(PaymentMethod.BankTransfer));
    }

    [Theory]
    [InlineData("Production", true, "Simulation", false)]
    [InlineData("Unknown", true, "Simulation", false)]
    [InlineData("Development", false, "Simulation", false)]
    [InlineData("Development", true, "Disabled", false)]
    [InlineData("Development", true, "Simulation", true)]
    [InlineData("Staging", true, "Simulation", true)]
    [InlineData("Testing", true, "Simulation", true)]
    public void SimulationRequiresBothExplicitConfigurationAndSafeEnvironment(string environment, bool enabled, string mode, bool expected)
    {
        var runtime = new PaymentRuntime(Options.Create(new PaymentOptions { Mode = mode, EnableSimulation = enabled }), new(environment));
        Assert.Equal(expected, runtime.GetCapabilities().Enabled);
        Assert.Equal(expected, runtime.GetCapabilities().IsTest);
        if (!expected) Assert.Throws<BusinessRuleException>(runtime.EnsureEnabled);
        Assert.Throws<BusinessRuleException>(() => runtime.EnsureSimulation(false));
    }

    [Theory]
    [InlineData(ProviderOperationStatus.Failed, PaymentStatus.Failed)]
    [InlineData(ProviderOperationStatus.Cancelled, PaymentStatus.Cancelled)]
    [InlineData(ProviderOperationStatus.Pending, PaymentStatus.Pending)]
    public void NonSuccessNeverConfirmsReservation(ProviderOperationStatus outcome, PaymentStatus status)
    {
        var (payment, reservation) = Pending();
        ProviderPaymentPolicy.Apply(payment, reservation, outcome, Now, 10);
        Assert.Equal(StatusIds.Build(StatusType.PaymentStatus, (int)status), payment.PaymentStatusId);
        Assert.Equal(StatusIds.Reservation(ReservationStatus.PendingPayment), reservation.StatusId);
        Assert.Null(payment.PaidAt);
        Assert.Null(reservation.ConfirmedAt);
        Assert.False(ProviderPaymentPolicy.Apply(payment, reservation, outcome, Now.AddMinutes(1), 10));
    }

    [Fact]
    public void SuccessConfirmsOnceAndPreservesCancellationPolicyAndAmounts()
    {
        var (payment, reservation) = Pending();
        Assert.True(ProviderPaymentPolicy.Apply(payment, reservation, ProviderOperationStatus.Succeeded, Now, 10));
        Assert.Equal(StatusIds.Reservation(ReservationStatus.Confirmed), reservation.StatusId);
        Assert.Equal(Now, payment.PaidAt);
        Assert.Equal(Now.AddMinutes(10), reservation.CancellationGraceDeadlineAt);
        Assert.Equal(Now.AddHours(1), reservation.CancellationDeadline);
        Assert.Equal(20, reservation.DepositAmount);
        Assert.True(ReservationCancellationRules.CanRefund(reservation, Now.AddMinutes(5), true));
        Assert.False(ProviderPaymentPolicy.Apply(payment, reservation, ProviderOperationStatus.Succeeded, Now.AddHours(1), 10));
        Assert.Equal(Now, payment.PaidAt);
        Assert.Single(reservation.StatusHistory);
    }

    [Theory]
    [InlineData(ReservationStatus.PendingPayment)]
    [InlineData(ReservationStatus.Cancelled)]
    [InlineData(ReservationStatus.Expired)]
    [InlineData(ReservationStatus.NoShow)]
    public void LateSuccessRecordsMoneyButNeverReactivatesBooking(ReservationStatus status)
    {
        var (payment, reservation) = Pending();
        reservation.StatusId = StatusIds.Reservation(status);
        reservation.HoldExpiresAt = Now;
        Assert.True(ProviderPaymentPolicy.Apply(payment, reservation, ProviderOperationStatus.Succeeded, Now, 10));
        Assert.Equal(StatusIds.Reservation(status), reservation.StatusId);
        Assert.True(payment.RequiresRefund);
        Assert.Equal(Now, payment.PaidAt);
        Assert.Null(reservation.ConfirmedAt);
    }

    [Theory]
    [InlineData("other", 20, "AZN", true)]
    [InlineData("original", 21, "AZN", true)]
    [InlineData("original", 20, "USD", true)]
    [InlineData("original", 20, "AZN", false)]
    public void ProviderIdentityAmountCurrencyAndModeMustMatch(string reference, int amount, string currency, bool test)
    {
        var (payment, _) = Pending();
        Assert.Throws<BusinessRuleException>(() => ProviderPaymentPolicy.ValidateResult(payment, reference, amount, currency, test));
    }

    [Theory]
    [InlineData(ProviderOperationStatus.Pending, RefundStatus.Processing)]
    [InlineData(ProviderOperationStatus.Failed, RefundStatus.Failed)]
    [InlineData(ProviderOperationStatus.Succeeded, RefundStatus.Refunded)]
    public void RefundOnlyCompletesAfterVerifiedSuccess(ProviderOperationStatus outcome, RefundStatus expected)
    {
        var (payment, _) = Pending();
        payment.PaymentStatusId = StatusIds.Build(StatusType.PaymentStatus, (int)PaymentStatus.Paid);
        payment.PaidAt = Now.AddMinutes(-2);
        var refund = new ReservationRefund { SourcePaymentId = payment.Id, ProviderName = payment.ProviderName,
            ProviderRef = "refund", Amount = 20, CurrencyCode = "AZN", StatusId = StatusIds.Refund(RefundStatus.Processing) };
        ProviderRefundPolicy.Apply(refund, payment, outcome, "refund", 20, "AZN", true, Now);
        Assert.Equal(StatusIds.Refund(expected), refund.StatusId);
        Assert.Equal(outcome == ProviderOperationStatus.Succeeded, refund.RefundedAt.HasValue);
        Assert.Equal(outcome == ProviderOperationStatus.Succeeded, payment.RefundedAt.HasValue);
        Assert.False(ProviderRefundPolicy.Apply(refund, payment, outcome, "refund", 20, "AZN", true, Now.AddMinutes(1)));
    }

    [Fact]
    public void RefundCannotReferenceAnotherPaymentOrExceedPaidAmount()
    {
        var (payment, _) = Pending();
        var refund = new ReservationRefund { SourcePaymentId = 999, ProviderName = "simulation", ProviderRef = "refund", Amount = 21, CurrencyCode = "AZN" };
        Assert.Throws<BusinessRuleException>(() => ProviderRefundPolicy.Apply(refund, payment, ProviderOperationStatus.Succeeded, "refund", 21, "AZN", true, Now));
        refund.SourcePaymentId = payment.Id;
        Assert.Throws<BusinessRuleException>(() => ProviderRefundPolicy.Apply(refund, payment, ProviderOperationStatus.Succeeded, "refund", 21, "AZN", true, Now));
    }

    [Theory]
    [InlineData(nameof(ReservationFlowController.SubmitPaymentProof), typeof(ReservationFlowController))]
    [InlineData(nameof(ReservationFlowController.SendPaymentInstruction), typeof(ReservationFlowController))]
    [InlineData(nameof(ReservationFlowController.ApprovePaymentProof), typeof(ReservationFlowController))]
    [InlineData(nameof(ReservationFlowController.RejectPaymentProof), typeof(ReservationFlowController))]
    [InlineData(nameof(ReservationRefundFlowController.SubmitRefundPayoutDetails), typeof(ReservationRefundFlowController))]
    [InlineData(nameof(ReservationRefundFlowController.SubmitRefundTransfer), typeof(ReservationRefundFlowController))]
    [InlineData(nameof(ReservationRefundFlowController.ConfirmRefundTransfer), typeof(ReservationRefundFlowController))]
    [InlineData(nameof(ReservationRefundFlowController.DisputeRefundTransfer), typeof(ReservationRefundFlowController))]
    [InlineData(nameof(ReservationRefundController.GetRefundPayoutDetails), typeof(ReservationRefundController))]
    public void EveryManualPaymentTransportIsRetiredBeforeModelBinding(string method, Type controller)
        => Assert.NotNull(controller.GetMethod(method)!.GetCustomAttribute<RetiredManualPaymentAttribute>());

    [Theory]
    [InlineData("cardNumber")]
    [InlineData("cvv")]
    [InlineData("iban")]
    [InlineData("amount")]
    public void TestOutcomeDtoRejectsUnknownPaymentInformation(string field)
        => Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<SimulatePaymentRequest>(
            $"{{\"outcome\":\"Succeeded\",\"{field}\":\"sensitive\"}}", new JsonSerializerOptions(JsonSerializerDefaults.Web)));

    [Fact]
    public void TestOutcomeIsRequiredAndManualTransferIsNotAnOfferedPaymentMethod()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<SimulatePaymentRequest>("{}", new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        Assert.False(PaymentMethodPolicy.IsMvpSupported(PaymentMethod.BankTransfer));
        Assert.Equal([PaymentMethod.Online], PaymentMethodPolicy.MvpSupportedMethods);
    }

    [Fact]
    public void RefundResponseNeverExposesLegacyPayoutOrProofData()
    {
        var refund = new ReservationRefund { Reservation = new Reservation(), StatusId = StatusIds.Refund(RefundStatus.AwaitingPayoutDetails),
            EligibilityReason = "eligible", RequestedAt = Now, PayoutDetails = new ReservationRefundPayoutDetail
            { EncryptedDetails = "secret", MaskedDetails = "legacy-account" },
            TransferAttempts = [new ReservationRefundTransfer { ProofFileId = 15, TransferReference = "private-reference" }] };
        var response = ReservationRefundResponseMapper.Map(refund, Mock.Of<IFileAccessUrlService>());
        Assert.Null(response.PayoutDetails);
        Assert.Null(response.LatestTransfer);
        Assert.Empty(response.TransferAttempts);
        Assert.True(response.RequiresManualReview);
        Assert.False(response.CanSimulate);
        var json = JsonSerializer.Serialize(response);
        Assert.DoesNotContain("secret", json);
        Assert.DoesNotContain("legacy-account", json);
        Assert.DoesNotContain("private-reference", json);
    }

    /// <summary>Sabit UTC anı və gələcək rezervasiya ilə kart məlumatı olmayan test ödənişi qurur.</summary>
    private static (Payment Payment, Reservation Reservation) Pending()
    {
        var reservation = new Reservation { Id = 1, RestaurantId = 1, DepositAmount = 20,
            StatusId = StatusIds.Reservation(ReservationStatus.PendingPayment), HoldExpiresAt = Now.AddMinutes(15),
            ReservedAt = Now.AddHours(2), CancellationDeadline = Now.AddHours(1) };
        var payment = new Payment { Id = 1, ReservationId = 1, RestaurantId = 1, Amount = 20, IsTest = true,
            ProviderName = "simulation", ProviderRef = "original", CurrencyCode = "AZN",
            PaymentStatusId = StatusIds.Build(StatusType.PaymentStatus, (int)PaymentStatus.Pending) };
        reservation.Payments.Add(payment);
        return (payment, reservation);
    }
}
