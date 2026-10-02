using ECafe.Application.Services.ReservationRefund.Concrete;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Infrastructure.Context;
using ECafe.Infrastructure.Repositories.ReservationRefund;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ECafe.Tests;

public sealed class RefundRequestInformationTests
{
    private static readonly DateTime Now = new(2026, 10, 1, 14, 33, 0, DateTimeKind.Utc);

    public static Reservation CreateReservation() => new()
    {
        Id = 1, RestaurantId = 2, TableId = 3, CustomerUserId = 4,
        StatusId = StatusIds.Reservation(ReservationStatus.Cancelled),
        Restaurant = new Restaurant { TimeZone = "Asia/Baku" },
        CancelledAt = Now,
        RefundEligible = true,
        PaymentProofs = [new ReservationPaymentProof
        {
            Id = 9, Amount = 20, SubmittedAt = Now.AddMinutes(-6),
            StatusId = StatusIds.Reservation(ReservationStatus.Confirmed)
        }]
    };

    [Fact]
    public void Eligible_cancellation_shows_amount_and_request_prompt_without_a_deadline()
    {
        var reservation = CreateReservation();
        var info = ReservationRefundRequestMapper.Map(reservation);
        Assert.NotNull(info);
        Assert.Contains("20.00 AZN", info.Message);
        Assert.Contains("müraciət edə bilərsiniz", info.Message);
        Assert.DoesNotContain("tarixinədək", info.Message);
    }

    [Fact]
    public void Passing_the_original_booking_and_cancellation_times_does_not_remove_a_saved_refund_right()
    {
        var reservation = CreateReservation();
        reservation.ReservedAt = DateTime.UtcNow.AddYears(-1);
        reservation.CancelledAt = reservation.ReservedAt.AddHours(-2);
        reservation.CancellationDeadline = reservation.ReservedAt.AddHours(-1);
        var info = ReservationRefundRequestMapper.Map(reservation);
        Assert.NotNull(info);
        Assert.Contains("20.00 AZN", info.Message);
    }

    [Fact]
    public void Starting_a_refund_removes_the_duplicate_request_prompt()
    {
        var reservation = CreateReservation();
        reservation.Refunds.Add(new ReservationRefund());
        Assert.Null(ReservationRefundRequestMapper.Map(reservation));
    }

    [Fact]
    public void Ineligible_or_unpaid_cancellation_never_promises_a_refund()
    {
        var reservation = CreateReservation();
        reservation.RefundEligible = false;
        Assert.Null(ReservationRefundRequestMapper.Map(reservation));
        reservation.RefundEligible = true;
        reservation.PaymentProofs.Clear();
        Assert.Null(ReservationRefundRequestMapper.Map(reservation));
    }

    [Fact]
    public void Refund_notice_uses_the_confirmed_payment_amount_not_an_unapproved_receipt()
    {
        var reservation = CreateReservation();
        reservation.DepositAmount = 100;
        reservation.PaymentProofs.Add(new ReservationPaymentProof
        {
            Id = 10, Amount = 100, SubmittedAt = Now,
            StatusId = StatusIds.Reservation(ReservationStatus.PaymentSubmitted)
        });
        var info = ReservationRefundRequestMapper.Map(reservation);
        Assert.NotNull(info);
        Assert.Equal(20, info.Amount);
        Assert.DoesNotContain("100.00", info.Message);
    }

    [Theory]
    [InlineData(RefundStatus.AwaitingPayoutDetails, "rekvizitlərinizi")]
    [InlineData(RefundStatus.ReadyForPayout, "köçürməsini")]
    [InlineData(RefundStatus.Processing, "yalnız bundan sonra")]
    [InlineData(RefundStatus.Refunded, "Əlavə sorğu")]
    public void Next_step_matches_the_refund_stage(RefundStatus status, string expected)
        => Assert.Contains(expected, ReservationRefundRequestMapper.GetCustomerNextStep(StatusIds.Refund(status)));

    [Theory]
    [InlineData(0, true, false, true)]
    [InlineData(365, true, false, true)]
    [InlineData(365, false, false, false)]
    [InlineData(365, true, true, false)]
    public async Task Workflow_availability_respects_saved_eligibility_and_existing_requests_regardless_of_elapsed_days(
        int elapsedDays, bool eligible, bool alreadyRequested, bool expected)
    {
        var options = new DbContextOptionsBuilder<ECafeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new ECafeDbContext(options);
        var reservation = new Reservation
        {
            Id = 1, RestaurantId = 2, TableId = 3, CustomerUserId = 4, PeopleCount = 2,
            RefundEligible = eligible,
            CancelledAt = DateTime.UtcNow.AddDays(-elapsedDays)
        };
        db.Reservations.Add(reservation);
        if (alreadyRequested)
            db.ReservationRefunds.Add(new ReservationRefund
            {
                Id = 7, ReservationId = 1, CurrencyCode = "AZN", EligibilityReason = "Eligible"
            });
        await db.SaveChangesAsync();
        var repository = new ReservationRefundRepository(db);
        Assert.Equal(expected, await repository.IsRequestAvailableAsync(2, 1));
        Assert.False(await repository.IsRequestAvailableAsync(99, 1));
    }
}
