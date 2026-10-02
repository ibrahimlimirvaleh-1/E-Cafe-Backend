using System.Data;
using AutoMapper;
using ECafe.Application.DTOs.Reservation;
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
using ECafe.Application.Services.Reservation.Concrete;
using ECafe.Application.Services.Restaurant.Abstract;
using ECafe.Application.Services.Workflow.Abstract;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace ECafe.Tests;

public sealed class ReservationNoShowServiceTests
{
    private static ReservationManager CreateManager(
        IReservationRepository reservations,
        ITableRepository tables,
        IApplicationDbTransactionFactory transactions)
        => new(
            Mock.Of<IHttpContextAccessor>(), Mock.Of<IMapper>(), Mock.Of<IConfiguration>(),
            tables, Mock.Of<ITableSessionRepository>(), Mock.Of<IRestaurantRepository>(),
            Mock.Of<IRestaurantDepositService>(), Mock.Of<IRestaurantContractRepository>(),
            reservations, Mock.Of<IUserRestaurantRepository>(), Mock.Of<INotificationService>(),
            Mock.Of<IAuditLogService>(), transactions,
            Mock.Of<IReservationPaymentInstructionRepository>(), Mock.Of<IReservationPaymentProofRepository>(),
            Mock.Of<IFileRepository>(), Mock.Of<IFileAccessUrlService>(), Mock.Of<IWorkflowActionService>(),
            Mock.Of<IPaymentInstructionDetailsProtector>(), Mock.Of<ILogger<ReservationManager>>(),
            Options.Create(new ReservationTimingOptions()));

    [Theory]
    [InlineData(true, 1)]
    [InlineData(false, 0)]
    public async Task WaitsForTableLockBeforeRechecking_AndCountsOnlyActualTransitions(bool expired, int expectedCount)
    {
        var candidate = new ReservationNoShowCandidate(1, 2, 3);
        var reservations = new Mock<IReservationRepository>();
        reservations.Setup(r => r.GetPendingExpiryCandidatesAsync(It.IsAny<DateTime>(), 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        reservations.Setup(r => r.GetNoShowCandidatesAsync(It.IsAny<DateTime>(), 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync([candidate]);
        var order = new List<string>();
        var lockGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tables = new Mock<ITableRepository>();
        tables.Setup(t => t.AcquireReservationLockAsync(2, 3, It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("lock"))
            .Returns(lockGate.Task);
        var transaction = new Mock<IApplicationDbTransaction>();
        transaction.Setup(t => t.CommitAsync(It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("commit")).Returns(Task.CompletedTask);
        transaction.Setup(t => t.DisposeAsync()).Returns(ValueTask.CompletedTask);
        var transactions = new Mock<IApplicationDbTransactionFactory>();
        transactions.Setup(t => t.BeginTransactionAsync(IsolationLevel.ReadCommitted, It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("begin")).ReturnsAsync(transaction.Object);
        var releasedAt = DateTime.MaxValue;
        reservations.Setup(r => r.TryExpireNoShowReservationAsync(candidate, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Callback<ReservationNoShowCandidate, DateTime, CancellationToken>((_, now, _) =>
            {
                Assert.True(now >= releasedAt);
                order.Add("recheck");
            }).ReturnsAsync(expired);

        var resultTask = CreateManager(reservations.Object, tables.Object, transactions.Object)
            .ExpirePendingReservationsAsync(100, CancellationToken.None);

        Assert.Equal(new[] { "begin", "lock" }, order);
        reservations.Verify(r => r.TryExpireNoShowReservationAsync(
            candidate, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
        releasedAt = DateTime.UtcNow;
        lockGate.SetResult();

        Assert.Equal(expectedCount, await resultTask);
        Assert.Equal(new[] { "begin", "lock", "recheck", "commit" }, order);
        transaction.Verify(t => t.DisposeAsync(), Times.Once);
    }

    [Fact]
    public async Task CancellationWhileWaitingForLock_DoesNotExpireOrCommit()
    {
        using var cancellation = new CancellationTokenSource();
        var candidate = new ReservationNoShowCandidate(1, 2, 3);
        var reservations = new Mock<IReservationRepository>();
        reservations.Setup(r => r.GetPendingExpiryCandidatesAsync(It.IsAny<DateTime>(), 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        reservations.Setup(r => r.GetNoShowCandidatesAsync(It.IsAny<DateTime>(), 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync([candidate]);
        var tables = new Mock<ITableRepository>();
        tables.Setup(t => t.AcquireReservationLockAsync(2, 3, cancellation.Token))
            .Returns(Task.Delay(Timeout.Infinite, cancellation.Token));
        var transaction = new Mock<IApplicationDbTransaction>();
        transaction.Setup(t => t.DisposeAsync()).Returns(ValueTask.CompletedTask);
        var transactions = new Mock<IApplicationDbTransactionFactory>();
        transactions.Setup(t => t.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellation.Token))
            .ReturnsAsync(transaction.Object);

        var resultTask = CreateManager(reservations.Object, tables.Object, transactions.Object)
            .ExpirePendingReservationsAsync(100, cancellation.Token);
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => resultTask);
        reservations.Verify(r => r.TryExpireNoShowReservationAsync(
            It.IsAny<ReservationNoShowCandidate>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
        transaction.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        transaction.Verify(t => t.DisposeAsync(), Times.Once);
    }

    [Theory]
    [InlineData(true, 1)]
    [InlineData(false, 0)]
    public async Task PendingExpiryWaitsForLockAndRechecksBeforeCommitting(bool expired, int expectedCount)
    {
        var candidate = new ReservationPendingExpiryCandidate(1, 2, 3);
        var reservations = new Mock<IReservationRepository>();
        reservations.Setup(r => r.GetPendingExpiryCandidatesAsync(It.IsAny<DateTime>(), 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync([candidate]);
        reservations.Setup(r => r.GetNoShowCandidatesAsync(It.IsAny<DateTime>(), 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var order = new List<string>();
        var lockGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tables = new Mock<ITableRepository>();
        tables.Setup(t => t.AcquireReservationLockAsync(2, 3, It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("lock")).Returns(lockGate.Task);
        var transaction = new Mock<IApplicationDbTransaction>();
        transaction.Setup(t => t.CommitAsync(It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("commit")).Returns(Task.CompletedTask);
        transaction.Setup(t => t.DisposeAsync()).Returns(ValueTask.CompletedTask);
        var transactions = new Mock<IApplicationDbTransactionFactory>();
        transactions.Setup(t => t.BeginTransactionAsync(IsolationLevel.ReadCommitted, It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("begin")).ReturnsAsync(transaction.Object);
        var releasedAt = DateTime.MaxValue;
        reservations.Setup(r => r.TryExpirePendingReservationAsync(candidate, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Callback<ReservationPendingExpiryCandidate, DateTime, CancellationToken>((_, now, _) =>
            {
                Assert.True(now >= releasedAt);
                order.Add("recheck");
            }).ReturnsAsync(expired);

        var resultTask = CreateManager(reservations.Object, tables.Object, transactions.Object)
            .ExpirePendingReservationsAsync(100, CancellationToken.None);

        Assert.Equal(new[] { "begin", "lock" }, order);
        reservations.Verify(r => r.TryExpirePendingReservationAsync(
            candidate, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
        releasedAt = DateTime.UtcNow;
        lockGate.SetResult();

        Assert.Equal(expectedCount, await resultTask);
        Assert.Equal(new[] { "begin", "lock", "recheck", "commit" }, order);
        transaction.Verify(t => t.DisposeAsync(), Times.Once);
    }

    [Fact]
    public async Task PendingExpiryCancellationWhileWaitingForLockDoesNotExpireOrCommit()
    {
        using var cancellation = new CancellationTokenSource();
        var candidate = new ReservationPendingExpiryCandidate(1, 2, 3);
        var reservations = new Mock<IReservationRepository>();
        reservations.Setup(r => r.GetPendingExpiryCandidatesAsync(It.IsAny<DateTime>(), 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync([candidate]);
        var tables = new Mock<ITableRepository>();
        tables.Setup(t => t.AcquireReservationLockAsync(2, 3, cancellation.Token))
            .Returns(Task.Delay(Timeout.Infinite, cancellation.Token));
        var transaction = new Mock<IApplicationDbTransaction>();
        transaction.Setup(t => t.DisposeAsync()).Returns(ValueTask.CompletedTask);
        var transactions = new Mock<IApplicationDbTransactionFactory>();
        transactions.Setup(t => t.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellation.Token))
            .ReturnsAsync(transaction.Object);

        var resultTask = CreateManager(reservations.Object, tables.Object, transactions.Object)
            .ExpirePendingReservationsAsync(100, cancellation.Token);
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => resultTask);
        reservations.Verify(r => r.TryExpirePendingReservationAsync(
            It.IsAny<ReservationPendingExpiryCandidate>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
        transaction.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        transaction.Verify(t => t.DisposeAsync(), Times.Once);
    }
}
