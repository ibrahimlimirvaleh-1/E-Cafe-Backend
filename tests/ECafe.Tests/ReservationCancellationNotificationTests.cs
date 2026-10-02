using System.Data;
using System.Security.Claims;
using System.Text.Json;
using AutoMapper;
using ECafe.Application.DTOs.Notification;
using ECafe.Application.Repositories.File;
using ECafe.Application.Repositories.Reservation;
using ECafe.Application.Repositories.ReservationPaymentInstruction;
using ECafe.Application.Repositories.ReservationPaymentProof;
using ECafe.Application.Repositories.Restaurant;
using ECafe.Application.Repositories.RestaurantContract;
using ECafe.Application.Repositories.Table;
using ECafe.Application.Repositories.TableSession;
using ECafe.Application.Repositories.UserRestaurant;
using ECafe.Application.Repository;
using ECafe.Application.Services.AuditLog.Abstract;
using ECafe.Application.Services.FileAccess.Abstract;
using ECafe.Application.Services.Notification.Abstract;
using ECafe.Application.Services.PaymentInstructionDetails.Abstract;
using ECafe.Application.Services.Realtime.Abstract;
using ECafe.Application.Services.Reservation.Concrete;
using ECafe.Application.Services.Restaurant.Abstract;
using ECafe.Application.Services.Workflow.Abstract;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Infrastructure.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace ECafe.Tests;

public sealed class ReservationCancellationNotificationTests
{
    private sealed class Fixture
    {
        public Reservation Reservation { get; } = RefundRequestInformationTests.CreateReservation();
        public Mock<INotificationService> Notifications { get; } = new();
        public Mock<IApplicationDbTransaction> Transaction { get; } = new();
        public List<CreateNotificationRequest> Messages { get; } = [];
        public List<string> Events { get; } = [];
        public ReservationManager Service { get; }

        public Fixture(bool restaurantCancels = false)
        {
            var now = DateTime.UtcNow;
            Reservation.StatusId = StatusIds.Reservation(ReservationStatus.Confirmed);
            Reservation.ReservedAt = now.AddHours(1);
            Reservation.CancellationDeadline = now.AddHours(-1);
            Reservation.CancellationGraceDeadlineAt = now.AddMinutes(10);
            Reservation.RefundEligible = null;
            Reservation.CancelledAt = null;
            var reservations = new Mock<IReservationRepository>();
            reservations.Setup(r => r.GetByIdForCustomerSnapshotAsync(1, 4, It.IsAny<CancellationToken>())).ReturnsAsync(Reservation);
            reservations.Setup(r => r.GetByIdForCustomerForUpdateAsync(1, 2, 4, It.IsAny<CancellationToken>())).ReturnsAsync(Reservation);
            reservations.Setup(r => r.GetByIdForRestaurantSnapshotAsync(1, 2, It.IsAny<CancellationToken>())).ReturnsAsync(Reservation);
            reservations.Setup(r => r.GetByIdForRestaurantAsync(1, 2, It.IsAny<CancellationToken>())).ReturnsAsync(Reservation);
            var assignments = new Mock<IUserRestaurantRepository>();
            assignments.Setup(r => r.GetActiveByRestaurantAndRolesAsync(2, It.IsAny<IReadOnlyCollection<int>>()))
                .ReturnsAsync([new UserRestaurant { UserId = 5 }]);
            Transaction.Setup(t => t.DisposeAsync()).Returns(ValueTask.CompletedTask);
            Transaction.Setup(t => t.CommitAsync(It.IsAny<CancellationToken>()))
                .Callback(() => Events.Add("commit")).Returns(Task.CompletedTask);
            var transactions = new Mock<IApplicationDbTransactionFactory>();
            transactions.Setup(t => t.BeginTransactionAsync(IsolationLevel.ReadCommitted, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Transaction.Object);
            Notifications.Setup(n => n.CreateAsync(It.IsAny<CreateNotificationRequest>()))
                .Callback<CreateNotificationRequest>(request => { Messages.Add(request); Events.Add("persist"); })
                .Returns(Task.CompletedTask);
            var context = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([
                    new Claim("userId", "4"),
                    new Claim(ClaimTypes.Role, ((int)(restaurantCancels ? RoleCode.SuperAdmin : RoleCode.Customer)).ToString())
                ], "test"))
            };
            Service = new ReservationManager(new HttpContextAccessor { HttpContext = context },
                Mock.Of<IMapper>(), Mock.Of<IConfiguration>(), Mock.Of<ITableRepository>(),
                Mock.Of<ITableSessionRepository>(), Mock.Of<IRestaurantRepository>(), Mock.Of<IRestaurantDepositService>(),
                Mock.Of<IRestaurantContractRepository>(), reservations.Object, assignments.Object, Notifications.Object,
                Mock.Of<IAuditLogService>(), transactions.Object, Mock.Of<IReservationPaymentInstructionRepository>(),
                Mock.Of<IReservationPaymentProofRepository>(), Mock.Of<IFileRepository>(), Mock.Of<IFileAccessUrlService>(),
                Mock.Of<IWorkflowActionService>(), Mock.Of<IPaymentInstructionDetailsProtector>(),
                Mock.Of<ILogger<ReservationManager>>(), Options.Create(new ReservationTimingOptions()));
        }
    }

    [Fact]
    public async Task Customer_cancellation_stages_refund_notice_before_commit()
    {
        var f = new Fixture();
        var response = await f.Service.CancelReservationAsync(1, "Gələ bilmirəm");
        var message = Assert.Single(f.Messages, m => m.UserId == 4);
        Assert.Contains("20.00 AZN", message.Message);
        Assert.Contains("20.00 AZN", response.Message);
        using var payload = JsonDocument.Parse(message.PayloadJson!);
        Assert.Equal("refund", payload.RootElement.GetProperty("section").GetString());
        Assert.DoesNotContain("tarixinədək", message.Message);
        Assert.Equal(new[] { "persist", "persist", "commit" }, f.Events);
    }

    [Fact]
    public async Task Restaurant_cancellation_notifies_customer_with_reason_and_refund_information()
    {
        var f = new Fixture(restaurantCancels: true);
        f.Reservation.CancellationGraceDeadlineAt = null;
        await f.Service.CancelRestaurantReservationAsync(2, 1, "Restoran bağlıdır");
        var message = Assert.Single(f.Messages);
        Assert.Contains("Restoran bağlıdır", message.Message);
        Assert.Contains("20.00 AZN", message.Message);
        Assert.Equal(new[] { "persist", "commit" }, f.Events);
    }

    [Fact]
    public async Task A_failed_commit_never_sends_realtime_cancellation_or_refund_promises()
    {
        var databaseTransaction = new Mock<IDbContextTransaction>();
        databaseTransaction.Setup(t => t.CommitAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("rollback"));
        var dispatcher = new Mock<INotificationChangeDispatcher>();
        var transaction = new EfApplicationDbTransaction(databaseTransaction.Object, dispatcher.Object);
        await Assert.ThrowsAsync<InvalidOperationException>(() => transaction.CommitAsync());
        dispatcher.Verify(d => d.FlushAsync(It.IsAny<CancellationToken>()), Times.Never);
        await transaction.DisposeAsync();
        dispatcher.Verify(d => d.Discard(), Times.Once);
    }

    [Fact]
    public async Task Realtime_is_flushed_only_after_the_database_commit()
    {
        var sequence = new MockSequence();
        var databaseTransaction = new Mock<IDbContextTransaction>(MockBehavior.Strict);
        var dispatcher = new Mock<INotificationChangeDispatcher>(MockBehavior.Strict);
        databaseTransaction.InSequence(sequence).Setup(t => t.CommitAsync(CancellationToken.None)).Returns(Task.CompletedTask);
        dispatcher.InSequence(sequence).Setup(d => d.FlushAsync(CancellationToken.None)).Returns(Task.CompletedTask);
        await new EfApplicationDbTransaction(databaseTransaction.Object, dispatcher.Object).CommitAsync();
        dispatcher.Verify(d => d.FlushAsync(CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task Unpaid_cancellation_never_advertises_a_refund_deadline()
    {
        var f = new Fixture();
        f.Reservation.PaymentProofs.Clear();
        await f.Service.CancelReservationAsync(1, null);
        var message = Assert.Single(f.Messages, m => m.UserId == 4);
        Assert.DoesNotContain("geri ödəniş hüququnuz var", message.Message);
        Assert.False(f.Reservation.RefundEligible);
    }
}
