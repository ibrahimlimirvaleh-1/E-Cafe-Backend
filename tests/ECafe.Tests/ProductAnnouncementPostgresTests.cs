using System.Globalization;
using System.Security.Claims;
using ECafe.Application.Common.Audit;
using ECafe.Application.Common.Exceptions;
using ECafe.Application.DTOs.ProductAnnouncements;
using ECafe.Application.Services.ProductAnnouncements;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using ECafe.Domain.Policies;
using ECafe.Infrastructure.Context;
using ECafe.Infrastructure.Migrations;
using ECafe.Infrastructure.Repositories;
using ECafe.Infrastructure.Repositories.ProductAnnouncements;
using ECafe.Infrastructure.Repositories.User;
using ECafe.Infrastructure.Repositories.UserRefreshToken;
using ECafe.Infrastructure.Repositories.UserRestaurant;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace ECafe.Tests;

public sealed partial class MobileAuthPostgresTests
{
    [StockPostgresFact]
    public async Task AnnouncementsSeedIsDraftAndPublishedReceiptsAreVersionedAcrossDevices()
    {
        await using var fixture = await Database.CreateAsync();
        await using (var db = fixture.Context())
            Assert.Empty((await Announcements(db).PendingAsync(default)).Items);
        await AnnouncementRoleAsync(fixture, RoleCode.SuperAdmin);
        AdminAnnouncement created;
        await using (var db = fixture.Context())
        {
            created = await Announcements(db, RoleCode.SuperAdmin).CreateAsync(AnnouncementDraft(), default);
            Assert.Equal(2, created.Id);
            Assert.False(created.IsPublished);
            var published = await Announcements(db, RoleCode.SuperAdmin).SetPublishedAsync(created.Id, new(1), true, default);
            Assert.Equal(1, published.Version);
        }
        await AnnouncementRoleAsync(fixture, RoleCode.Customer);
        var originalCulture = CultureInfo.CurrentUICulture;
        try
        {
            foreach (var language in new[] { "az", "en", "ru" })
            {
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(language);
                await using var db = fixture.Context();
                var card = Assert.Single((await Announcements(db).PendingAsync(default)).Items);
                Assert.Equal("Title " + language, card.Title);
            }
        }
        finally { CultureInfo.CurrentUICulture = originalCulture; }
        await Task.WhenAll(Enumerable.Range(0, 12).Select(async _ =>
        {
            await using var db = fixture.Context();
            await Announcements(db).AcknowledgeAsync(created.Id, new(1), default);
        }));
        await using (var db = fixture.Context())
        {
            Assert.Single(await db.ProductAnnouncementReceipts.ToListAsync());
            Assert.Empty((await Announcements(db, device: "other-device").PendingAsync(default)).Items);
        }
        await AnnouncementRoleAsync(fixture, RoleCode.SuperAdmin);
        await using (var db = fixture.Context())
        {
            var service = Announcements(db, RoleCode.SuperAdmin);
            await service.SetPublishedAsync(created.Id, new(1), false, default);
            await service.SetPublishedAsync(created.Id, new(1), true, default);
        }
        await AnnouncementRoleAsync(fixture, RoleCode.Customer);
        await using (var db = fixture.Context())
            Assert.Empty((await Announcements(db).PendingAsync(default)).Items);
        await AnnouncementRoleAsync(fixture, RoleCode.SuperAdmin);
        await using (var db = fixture.Context())
        {
            var service = Announcements(db, RoleCode.SuperAdmin);
            await service.SetPublishedAsync(created.Id, new(1), false, default);
            var updated = await service.UpdateAsync(created.Id, AnnouncementDraft() with { ExpectedVersion = 1 }, default);
            Assert.Equal(2, updated.Version);
            await service.SetPublishedAsync(created.Id, new(2), true, default);
        }
        await AnnouncementRoleAsync(fixture, RoleCode.Customer);
        await using (var db = fixture.Context())
        {
            Assert.Equal(2, Assert.Single((await Announcements(db).PendingAsync(default)).Items).Version);
            var audits = await db.Set<AuditLog>().Where(value => value.EntityName == AuditEntityTypes.ProductAnnouncement).ToListAsync();
            Assert.Equal(7, audits.Count);
        }
    }

    [StockPostgresFact]
    public async Task AnnouncementsBoundBatchExcludeDraftExpiredFutureAndWrongRole()
    {
        await using var fixture = await Database.CreateAsync();
        await using (var db = fixture.Context())
        {
            for (var i = 0; i < 5; i++) db.ProductAnnouncements.Add(AnnouncementEntity("CustomerOrders", 5));
            db.ProductAnnouncements.Add(AnnouncementEntity("CustomerOrders", 5, useDefaultPublished: false));
            db.ProductAnnouncements.Add(AnnouncementEntity("CustomerOrders", 5, expires: DateTime.UtcNow.AddDays(-1)));
            db.ProductAnnouncements.Add(AnnouncementEntity("CustomerOrders", 5, published: DateTime.UtcNow.AddDays(1)));
            db.ProductAnnouncements.Add(AnnouncementEntity("WaiterTables", 4));
            await db.SaveChangesAsync();
        }
        await using var read = fixture.Context();
        var batch = await Announcements(read).PendingAsync(default);
        Assert.Equal(3, batch.Items.Count);
        Assert.True(batch.HasMore);
        Assert.All(batch.Items, value => Assert.Equal("CustomerOrders", value.Destination));
        foreach (var card in batch.Items) await Announcements(read).AcknowledgeAsync(card.Id, new(card.Version), default);
        var remainder = await Announcements(read).PendingAsync(default);
        Assert.Equal(2, remainder.Items.Count);
        Assert.False(remainder.HasMore);
    }

    [StockPostgresFact]
    public async Task AnnouncementsValidateSelectedProfileSeparatelyFromGlobalRoleAndFreshPermissions()
    {
        await using var fixture = await Database.CreateAsync();
        await AnnouncementRoleAsync(fixture, RoleCode.Owner, RoleCode.Waiter);
        await using (var db = fixture.Context())
        {
            db.ProductAnnouncements.Add(AnnouncementEntity("WaiterTables", 4));
            await db.SaveChangesAsync();
        }
        await using (var db = fixture.Context())
        {
            var batch = await Announcements(db, RoleCode.Owner, RoleCode.Waiter, 1001).PendingAsync(default);
            Assert.Single(batch.Items);
            Assert.Equal(4, batch.RoleId);
            Assert.Equal(1001, batch.RestaurantId);
            await Assert.ThrowsAsync<ForbiddenException>(() => Announcements(db, RoleCode.Owner, RoleCode.Waiter, 2002).PendingAsync(default));
            await Assert.ThrowsAsync<ForbiddenException>(() => Announcements(db, RoleCode.Owner, RoleCode.Kitchen, 1001).PendingAsync(default));
            await Assert.ThrowsAsync<ForbiddenException>(() => Announcements(db, RoleCode.Owner).PendingAsync(default));
            await Assert.ThrowsAsync<ForbiddenException>(() => Announcements(db, RoleCode.Owner, RoleCode.SuperAdmin).AdminPageAsync(null, default));
        }
        await using (var db = fixture.Context())
        {
            db.Set<RolePermission>().RemoveRange(await db.Set<RolePermission>().Where(value => value.RoleId == 4 && value.PermissionId == 9).ToListAsync());
            await db.SaveChangesAsync();
        }
        await using (var db = fixture.Context())
            Assert.Empty((await Announcements(db, RoleCode.Owner, RoleCode.Waiter, 1001).PendingAsync(default)).Items);
        await using (var db = fixture.Context())
        {
            (await db.UserRestaurants.SingleAsync()).IsActive = false;
            await db.SaveChangesAsync();
        }
        await using var inactive = fixture.Context();
        var error = await Assert.ThrowsAsync<UnauthorizedException>(() => Announcements(inactive, RoleCode.Owner, RoleCode.Waiter, 1001).PendingAsync(default));
        Assert.Equal(ErrorCode.StaffProfileInactive, error.Code);
    }

    [StockPostgresFact]
    public async Task AnnouncementsRejectInactiveStaleOrForgedSessionAndCustomerWithStaffCannotSeeDeletion()
    {
        await using var fixture = await Database.CreateAsync();
        await using (var db = fixture.Context())
        {
            var row = await db.ProductAnnouncements.Include(value => value.Texts).SingleAsync();
            row.PublishedAtUtc = DateTime.UtcNow.AddSeconds(-1);
            await db.SaveChangesAsync();
        }
        await using (var db = fixture.Context())
        {
            Assert.Single((await Announcements(db).PendingAsync(default)).Items);
            await Assert.ThrowsAsync<UnauthorizedException>(() => Announcements(db, RoleCode.SuperAdmin).PendingAsync(default));
            await Assert.ThrowsAsync<UnauthorizedException>(() => Announcements(db, device: "forged").PendingAsync(default));
            await Assert.ThrowsAsync<UnauthorizedException>(() => Announcements(db, version: 99).PendingAsync(default));
            await Assert.ThrowsAsync<ForbiddenException>(() => Announcements(db, restaurant: 1001).PendingAsync(default));
        }
        await AnnouncementRoleAsync(fixture, RoleCode.Customer, RoleCode.Waiter);
        await using (var db = fixture.Context())
            Assert.Empty((await Announcements(db).PendingAsync(default)).Items);
        await using (var db = fixture.Context())
        {
            (await db.Users.SingleAsync()).IsActive = false;
            await db.SaveChangesAsync();
        }
        await using var inactive = fixture.Context();
        await Assert.ThrowsAsync<UnauthorizedException>(() => Announcements(inactive).PendingAsync(default));
    }

    [StockPostgresFact]
    public async Task AnnouncementsDraftValidationAndPublicationProtectContentAndVersion()
    {
        await using var fixture = await Database.CreateAsync();
        await AnnouncementRoleAsync(fixture, RoleCode.SuperAdmin);
        await using var db = fixture.Context();
        var service = Announcements(db, RoleCode.SuperAdmin);
        foreach (var invalid in new[]
        {
            AnnouncementDraft() with { Destination = "https://evil.example" },
            AnnouncementDraft() with { RoleIds = new[] { 4 } },
            AnnouncementDraft() with { RoleIds = new[] { 5, 5 } },
            AnnouncementDraft() with { Texts = new[] { new AnnouncementTextRequest("az", "x", "x") } },
            AnnouncementDraft() with { ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1) },
            AnnouncementDraft() with { Texts = AnnouncementDraft().Texts.Select(value => value with { Title = new string('a', 121) }).ToArray() }
        })
            await Assert.ThrowsAsync<BadRequestException>(() => service.CreateAsync(invalid, default));
        var empty = await service.CreateAsync(AnnouncementDraft() with { Texts = AnnouncementDraft().Texts.Select(value => value with { Body = "" }).ToArray() }, default);
        await Assert.ThrowsAsync<BadRequestException>(() => service.SetPublishedAsync(empty.Id, new(1), true, default));
        var card = await service.CreateAsync(AnnouncementDraft(), default);
        await service.SetPublishedAsync(card.Id, new(1), true, default);
        var publishedEdit = await Assert.ThrowsAsync<BusinessRuleException>(() => service.UpdateAsync(card.Id, AnnouncementDraft() with { ExpectedVersion = 1 }, default));
        Assert.Equal(ErrorCode.AnnouncementMustBeDraft, publishedEdit.Code);
        await service.SetPublishedAsync(card.Id, new(1), false, default);
        var stale = await Assert.ThrowsAsync<BusinessRuleException>(() => service.UpdateAsync(card.Id, AnnouncementDraft() with { ExpectedVersion = 99 }, default));
        Assert.Equal(ErrorCode.AnnouncementChanged, stale.Code);
        await service.UpdateAsync(card.Id, AnnouncementDraft() with { ExpectedVersion = 1,
            Texts = AnnouncementDraft().Texts.Select(value => value with { Body = "<script>alert(1)</script>" }).ToArray() }, default);
        await Assert.ThrowsAsync<BadRequestException>(() => service.SetPublishedAsync(card.Id, new(2), true, default));
    }

    [StockPostgresFact]
    public async Task AnnouncementsParallelAdminEditRejectsStaleFormAndRollbackKeepsDraft()
    {
        await using var fixture = await Database.CreateAsync();
        await AnnouncementRoleAsync(fixture, RoleCode.SuperAdmin);
        await using (var setup = fixture.Context())
        {
            setup.Users.Add(new User { Id = 1002, Name = "Other", Surname = "Admin", Email = "admin1002@example.test",
                Phone = "1002", Password = "test-only", RoleId = 1, IsActive = true });
            setup.UserRefreshTokens.Add(new UserRefreshToken { UserId = 1002, SessionId = Session,
                TokenHash = "announcement-other-admin-test-only", ExpiresAt = DateTime.UtcNow.AddDays(1) });
            await setup.SaveChangesAsync();
        }
        int id;
        await using (var db = fixture.Context()) id = (await Announcements(db, RoleCode.SuperAdmin).CreateAsync(AnnouncementDraft(), default)).Id;
        var attempts = await Task.WhenAll(new[] { 1001, 1002 }.Select(async userId =>
        {
            await using var db = fixture.Context();
            return await AttemptAsync(async () => { await Announcements(db, RoleCode.SuperAdmin, userId: userId).UpdateAsync(id, AnnouncementDraft() with { ExpectedVersion = 1 }, default); });
        }));
        Assert.Single(attempts, value => value == null);
        Assert.Equal(ErrorCode.AnnouncementChanged, Assert.IsType<BusinessRuleException>(Assert.Single(attempts, value => value != null)).Code);
        await using (var db = fixture.Context(new FailingSave()))
            await Assert.ThrowsAsync<DbUpdateException>(() => Announcements(db, RoleCode.SuperAdmin).SetPublishedAsync(id, new(2), true, default));
        await using var read = fixture.Context();
        var stored = await read.ProductAnnouncements.SingleAsync(value => value.Id == id);
        Assert.Null(stored.PublishedAtUtc);
        Assert.Equal(2, stored.Version);
    }

    [StockPostgresFact]
    public async Task AnnouncementsAcknowledgementRejectsWrongRoleOrVersionButRetriesAfterUnpublish()
    {
        await using var fixture = await Database.CreateAsync();
        int id;
        await using (var db = fixture.Context())
        {
            var entity = AnnouncementEntity("CustomerOrders", 5);
            db.ProductAnnouncements.Add(entity);
            await db.SaveChangesAsync();
            id = entity.Id;
        }
        await using (var db = fixture.Context())
        {
            var wrong = await Assert.ThrowsAsync<BusinessRuleException>(() => Announcements(db).AcknowledgeAsync(id, new(99), default));
            Assert.Equal(ErrorCode.AnnouncementChanged, wrong.Code);
            await Assert.ThrowsAsync<NotFoundException>(() => Announcements(db).AcknowledgeAsync(1, new(1), default));
            await Announcements(db).AcknowledgeAsync(id, new(1), default);
        }
        await using (var db = fixture.Context())
        {
            (await db.ProductAnnouncements.SingleAsync(value => value.Id == id)).PublishedAtUtc = null;
            await db.SaveChangesAsync();
        }
        await using var retry = fixture.Context();
        await Announcements(retry).AcknowledgeAsync(id, new(1), default);
        Assert.Single(await retry.ProductAnnouncementReceipts.ToListAsync());
    }

    [StockPostgresFact]
    public async Task AnnouncementsAcknowledgeAndUnpublishSerializeInEitherOrder()
    {
        foreach (var acknowledgeFirst in new[] { true, false })
        {
            await using var fixture = await Database.CreateAsync();
            await AnnouncementRoleAsync(fixture, RoleCode.SuperAdmin);
            int id;
            await using (var setup = fixture.Context())
            {
                var row = AnnouncementEntity("AnnouncementAdmin", 1);
                setup.ProductAnnouncements.Add(row);
                await setup.SaveChangesAsync();
                id = row.Id;
            }
            var result = await RunOrderedAsync(fixture,
                db => acknowledgeFirst ? Announcements(db, RoleCode.SuperAdmin).AcknowledgeAsync(id, new(1), default)
                    : Announcements(db, RoleCode.SuperAdmin).SetPublishedAsync(id, new(1), false, default),
                db => acknowledgeFirst ? Announcements(db, RoleCode.SuperAdmin).SetPublishedAsync(id, new(1), false, default)
                    : Announcements(db, RoleCode.SuperAdmin).AcknowledgeAsync(id, new(1), default));
            Assert.Null(result.First);
            if (acknowledgeFirst) Assert.Null(result.Second);
            else Assert.IsType<NotFoundException>(result.Second);
            await using var read = fixture.Context();
            Assert.Equal(acknowledgeFirst ? 1 : 0, await read.ProductAnnouncementReceipts.CountAsync());
            Assert.Null((await read.ProductAnnouncements.SingleAsync(value => value.Id == id)).PublishedAtUtc);
        }
    }

    [StockPostgresFact]
    public async Task AnnouncementsAdminPaginationIsBoundedAndUnauthorizedCustomerCannotWrite()
    {
        await using var fixture = await Database.CreateAsync();
        await using (var db = fixture.Context())
        {
            await Assert.ThrowsAsync<ForbiddenException>(() => Announcements(db).CreateAsync(AnnouncementDraft(), default));
            await Assert.ThrowsAsync<ForbiddenException>(() => Announcements(db).DestinationsAsync(default));
            for (var i = 0; i < 32; i++) db.ProductAnnouncements.Add(AnnouncementEntity("CustomerOrders", 5));
            await db.SaveChangesAsync();
        }
        await AnnouncementRoleAsync(fixture, RoleCode.SuperAdmin);
        await using var read = fixture.Context();
        var page = await Announcements(read, RoleCode.SuperAdmin).AdminPageAsync(null, default);
        Assert.Equal(30, page.Items.Count);
        Assert.True(page.HasMore);
        var rest = await Announcements(read, RoleCode.SuperAdmin).AdminPageAsync(page.NextBeforeId, default);
        Assert.Equal(3, rest.Items.Count);
        Assert.False(rest.HasMore);
        Assert.Empty(page.Items.Select(value => value.Id).Intersect(rest.Items.Select(value => value.Id)));
        Assert.False(read.Database.HasPendingModelChanges());
    }

    [StockPostgresFact]
    public async Task AnnouncementsMigrationUpDownPreservesExistingAccountsAndStartsIdentityAfterSeed()
    {
        await using var fixture = await Database.CreateAsync();
        await using var db = fixture.Context();
        var migration = new RoleTargetedProductAnnouncements();
        var sql = db.GetService<IMigrationsSqlGenerator>();
        foreach (var command in sql.Generate(migration.DownOperations, db.Model))
            await db.Database.ExecuteSqlRawAsync(command.CommandText);
        Assert.Equal(1, await db.Users.CountAsync());
        Assert.Equal(2, await db.UserRefreshTokens.CountAsync());
        foreach (var command in sql.Generate(migration.UpOperations, db.Model))
            await db.Database.ExecuteSqlRawAsync(command.CommandText);
        Assert.Null((await db.ProductAnnouncements.SingleAsync()).PublishedAtUtc);
        Assert.Equal(3, await db.Set<ProductAnnouncementText>().CountAsync());
        var entity = AnnouncementEntity("CustomerOrders", 5);
        db.ProductAnnouncements.Add(entity);
        await db.SaveChangesAsync();
        Assert.Equal(2, entity.Id);
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [StockPostgresFact]
    public async Task AnnouncementsReceiptFollowsUserRoleNotRestaurantAndOtherRoleRemainsUnseen()
    {
        await using var fixture = await Database.CreateAsync();
        await AnnouncementRoleAsync(fixture, RoleCode.Owner, RoleCode.Manager);
        int id;
        await using (var db = fixture.Context())
        {
            var row = AnnouncementEntity("Inventory", (int)RoleCode.Manager);
            row.Roles.Add(new() { RoleId = (int)RoleCode.Owner });
            db.ProductAnnouncements.Add(row);
            db.Restaurants.Add(new Restaurant { Id = 1002, Name = "Other restaurant", Location = "Baku", Phone = "1002", IsActive = true });
            db.UserRestaurants.Add(new UserRestaurant { UserId = 1001, RestaurantId = 1002, RoleId = (int)RoleCode.Manager, IsActive = true });
            await db.SaveChangesAsync();
            id = row.Id;
        }
        await using (var db = fixture.Context())
            await Announcements(db, RoleCode.Owner, RoleCode.Manager, 1001).AcknowledgeAsync(id, new(1), default);
        await using (var db = fixture.Context())
        {
            Assert.Empty((await Announcements(db, RoleCode.Owner, RoleCode.Manager, 1002).PendingAsync(default)).Items);
            var first = await db.UserRestaurants.SingleAsync(value => value.RestaurantId == 1001);
            first.RoleId = (int)RoleCode.Owner;
            await db.SaveChangesAsync();
        }
        await using var otherRole = fixture.Context();
        Assert.Single((await Announcements(otherRole, RoleCode.Owner, RoleCode.Owner, 1001).PendingAsync(default)).Items);
        await Assert.ThrowsAsync<NotFoundException>(() => Announcements(otherRole, RoleCode.Owner, RoleCode.Owner, 1001).AcknowledgeAsync(1, new(1), default));
    }

    /// <summary>Yalnız GUID-li lokal test bazasında qlobal rol və ayrıca restoran profilini qurur.</summary>
    private static async Task AnnouncementRoleAsync(Database fixture, RoleCode global, RoleCode? selected = null)
    {
        await using var db = fixture.Context();
        (await db.Users.SingleAsync(value => value.Id == 1001)).RoleId = (int)global;
        var assignment = await db.UserRestaurants.SingleAsync();
        assignment.RoleId = (int)(selected ?? global);
        assignment.IsActive = selected.HasValue || UserProfileAccessPolicy.IsRestaurantRole((int)global);
        await db.SaveChangesAsync();
    }

    /// <summary>Həqiqi repository və transaction-ları sintetik JWT/profil ilə birləşdirir; Redis və canlı API istifadə etmir.</summary>
    private static ProductAnnouncementManager Announcements(ECafeDbContext db, RoleCode global = RoleCode.Customer,
        RoleCode? selected = null, int? restaurant = null, string device = Session, int version = 0, int userId = 1001)
    {
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("userId", userId.ToString(CultureInfo.InvariantCulture)), new Claim(ClaimTypes.Role, ((int)global).ToString(CultureInfo.InvariantCulture)),
            new Claim("sessionVersion", version.ToString(CultureInfo.InvariantCulture)), new Claim("sessionId", device)
        }, "test")) };
        if (selected.HasValue) context.Request.Headers["X-Active-Role-Id"] = ((int)selected.Value).ToString(CultureInfo.InvariantCulture);
        if (restaurant.HasValue) context.Request.Headers["X-Active-Restaurant-Id"] = restaurant.Value.ToString(CultureInfo.InvariantCulture);
        return new(new ProductAnnouncementRepository(db), new UserRepository(db), new UserRestaurantRepository(db),
            new UserRefreshTokenRepository(db), new BaseRepository<RolePermission>(db), new BaseRepository<AuditLog>(db),
            new Transactions(db), new HttpContextAccessor { HttpContext = context }, TimeProvider.System);
    }

    /// <summary>Üç dil və müştəri rolu olan minimal draft sorğusu yaradır.</summary>
    private static AnnouncementWriteRequest AnnouncementDraft() => new("CustomerOrders", new[] { 5 },
        new[] { "az", "en", "ru" }.Select(language => new AnnouncementTextRequest(language, "Title " + language, "Body " + language)).ToArray(), null);

    /// <summary>Feed filtrlərini sınamaq üçün yalnız test bazasına elan modeli verir.</summary>
    private static ProductAnnouncement AnnouncementEntity(string destination, int role, DateTime? expires = null,
        DateTime? published = null, bool useDefaultPublished = true) => new()
    {
        Destination = destination, Version = 1, PublishedAtUtc = published ?? (useDefaultPublished ? DateTime.UtcNow.AddSeconds(-1) : null),
        ExpiresAtUtc = expires, CreatedBy = "test", Texts = AnnouncementDraft().Texts.Select(value => new ProductAnnouncementText
        { Language = value.Language, Title = value.Title, Body = value.Body }).ToList(),
        Roles = new List<ProductAnnouncementRole> { new() { RoleId = role } }
    };
}
