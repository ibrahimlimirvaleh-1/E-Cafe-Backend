using ECafe.Application.DTOs.Notification;
using ECafe.Application.Repositories.UserRestaurant;
using ECafe.Application.Services.InventoryMovement.Concrete;
using ECafe.Application.Services.Notification.Abstract;
using ECafe.Domain.Entities;
using ECafe.Domain.Policies;
using Moq;
using Xunit;

namespace ECafe.Tests;

public sealed class InventoryLowStockNotifierTests
{
    [Fact]
    public async Task FirstCrossingNotifiesDistinctRestaurantStaffOnlyAndRecoveryAllowsNextCrossing()
    {
        var staff = new Mock<IUserRestaurantRepository>();
        staff.Setup(repository => repository.GetActiveByRestaurantAndRolesAsync(7,
            It.Is<IReadOnlyCollection<int>>(roles => roles.Contains(2) && roles.Contains(6))))
            .ReturnsAsync([new UserRestaurant { UserId = 10 }, new UserRestaurant { UserId = 11 }, new UserRestaurant { UserId = 10 }]);
        var notices = new List<CreateNotificationRequest>();
        var notifications = new Mock<INotificationService>();
        notifications.Setup(service => service.CreateAsync(It.IsAny<CreateNotificationRequest>()))
            .Callback<CreateNotificationRequest>(notices.Add).Returns(Task.CompletedTask);
        var notifier = new InventoryLowStockNotifier(staff.Object, notifications.Object, TimeProvider.System);
        var item = new InventoryItem { Id = 1, RestaurantId = 7, Name = "Flour", QuantityOnHand = 0.4m, LowStockThreshold = 0.5m };
        var unit = new Unit { Id = 1, Code = "kg" };
        await notifier.NotifyAsync(item, unit, 1);
        await notifier.NotifyAsync(item, unit, 0.4m);
        Assert.Equal(2, notices.Count);
        Assert.All(notices, notice => Assert.Equal(7, notice.RestaurantId));
        Assert.NotNull(item.LastLowStockNotifiedAt);
        InventoryStockPolicy.ApplyChange(item, 1);
        Assert.Null(item.LastLowStockNotifiedAt);
        InventoryStockPolicy.ApplyChange(item, -1);
        await notifier.NotifyAsync(item, unit, 1.4m);
        Assert.Equal(4, notices.Count);
    }
}
