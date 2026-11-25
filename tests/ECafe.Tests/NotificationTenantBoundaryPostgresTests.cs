using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Infrastructure.Repositories.Notification;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ECafe.Tests;

public sealed partial class MobileAuthPostgresTests
{
    [StockPostgresFact]
    public async Task NotificationSqlRechecksTenantAfterRevocationInAnotherConnection()
    {
        await using var fixture = await ProfileDatabaseAsync();
        await using (var seed = fixture.Context())
        {
            seed.Notifications.AddRange(TenantNotice(1001, 1001), TenantNotice(1002, 1002), TenantNotice(1003, null));
            await seed.SaveChangesAsync();
        }
        await using var reader = fixture.Context();
        var repository = new NotificationRepository(reader);
        Assert.Equal(2, (await repository.GetByUserAsync(1001, 1001)).Count);
        Assert.Equal(2, (await repository.GetPageByUserAsync(1001, 1001, 1, 20, default)).TotalCount);
        Assert.Empty(await repository.GetByUserAsync(1001, null));

        await using (var writer = fixture.Context())
            await DeactivateProfileAsync(writer, editForm: false);

        Assert.Empty(await repository.GetByUserAsync(1001, 1001));
        var revokedPage = await repository.GetPageByUserAsync(1001, 1001, 1, 20, default);
        Assert.Empty(revokedPage.Items);
        Assert.Equal(0, revokedPage.TotalCount);
        Assert.Equal(0, await repository.GetUnreadCountAsync(1001, 1001));
        Assert.Empty(await repository.GetUnreadByUserAsync(1001, 1001));
        Assert.Null(await repository.GetByUserAndIdTrackedAsync(1001, 1001, 1001));
        Assert.Equal(new[] { 1002, 1003 }, (await repository.GetByUserAsync(1001, 1002)).Select(value => value.Id).Order().ToArray());
        Assert.Equal(2, await repository.GetUnreadCountAsync(1001, 1002));
        Assert.Equal(2, (await repository.GetUnreadByUserAsync(1001, 1002)).Count);
        Assert.Null(await repository.GetByUserAndIdTrackedAsync(1001, 1001, 1002));
        Assert.NotNull(await repository.GetByUserAndIdTrackedAsync(1001, 1002, 1002));
        Assert.False((await reader.Notifications.SingleAsync(value => value.Id == 1001)).IsRead);
    }

    [StockPostgresFact]
    public async Task NotificationSqlKeepsOrdinaryCustomerAndAdminHistoryPrivateAcrossRestaurants()
    {
        foreach (var role in new[] { RoleCode.Customer, RoleCode.SuperAdmin })
        {
            await using var fixture = await ProfileDatabaseAsync();
            await using var db = fixture.Context();
            (await db.Users.SingleAsync(value => value.Id == 1001)).RoleId = (int)role;
            foreach (var assignment in await db.UserRestaurants.ToListAsync()) assignment.IsActive = false;
            db.Users.Add(new User { Id = 1002, Name = "Other", Surname = "Test", Email = "other@example.test", Phone = "1002",
                Password = "synthetic", RoleId = (int)RoleCode.Customer, IsActive = true });
            db.Notifications.AddRange(TenantNotice(1001, 1001), TenantNotice(1002, 1002), TenantNotice(1003, null),
                new Notification { Id = 1004, UserId = 1002, RestaurantId = 1001, Title = "Other owner", Message = "Synthetic",
                    StatusId = (int)NotificationStatus.Unread });
            await db.SaveChangesAsync();

            var repository = new NotificationRepository(db);
            Assert.Equal(new[] { 1001, 1002, 1003 }, (await repository.GetByUserAsync(1001, null)).Select(value => value.Id).Order().ToArray());
            Assert.Equal(3, await repository.GetUnreadCountAsync(1001, null));
            var page = await repository.GetPageByUserAsync(1001, null, 2, 2, default);
            Assert.Single(page.Items);
            Assert.Equal(3, page.TotalCount);
            Assert.DoesNotContain(page.Items, value => value.UserId != 1001);
            Assert.Null(await repository.GetByUserAndIdTrackedAsync(1001, 1004, null));
        }
    }

    [StockPostgresFact]
    public async Task NotificationSqlPaginationHasStableBoundariesAndDoesNotTrackOrCountOtherRestaurants()
    {
        await using var fixture = await ProfileDatabaseAsync();
        await using (var seed = fixture.Context())
        {
            seed.Notifications.AddRange(Enumerable.Range(2000, 45).Select(id => TenantNotice(id, 1001)));
            seed.Notifications.AddRange(TenantNotice(3000, null), TenantNotice(4000, 1002));
            await seed.SaveChangesAsync();
            foreach (var notification in await seed.Notifications.ToListAsync())
                notification.CreatedAt = new DateTime(2026, 11, 25, 8, 0, 0, DateTimeKind.Utc);
            await seed.SaveChangesAsync();
        }
        await using var db = fixture.Context();
        var repository = new NotificationRepository(db);
        var first = await repository.GetPageByUserAsync(1001, 1001, 1, 5, default);
        var second = await repository.GetPageByUserAsync(1001, 1001, 2, 5, default);
        Assert.Equal(new[] { 3000, 2044, 2043, 2042, 2041 }, first.Items.Select(value => value.Id));
        Assert.Equal(new[] { 2040, 2039, 2038, 2037, 2036 }, second.Items.Select(value => value.Id));
        Assert.Equal(46, first.TotalCount);
        Assert.Equal(10, first.TotalPages);
        Assert.Empty(first.Items.Select(value => value.Id).Intersect(second.Items.Select(value => value.Id)));
        Assert.Empty(db.ChangeTracker.Entries<Notification>());
        var beyond = await repository.GetPageByUserAsync(1001, 1001, int.MaxValue, 100, default);
        Assert.Empty(beyond.Items);
        Assert.Equal(46, beyond.TotalCount);
    }

    /// <summary>Həqiqi PostgreSQL sorğusunda istifadə edilən, öz istifadəçisinə aid sintetik bildiriş yaradır.</summary>
    private static Notification TenantNotice(int id, int? restaurantId) => new()
    {
        Id = id, UserId = 1001, RestaurantId = restaurantId, Title = "Tenant boundary", Message = "Synthetic only",
        StatusId = (int)NotificationStatus.Unread, IsRead = false
    };
}
