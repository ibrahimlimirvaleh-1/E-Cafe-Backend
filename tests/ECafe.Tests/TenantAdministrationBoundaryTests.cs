using System.Reflection;
using System.Security.Claims;
using ECafe.Api.Controllers;
using ECafe.Application.Common.Exceptions;
using ECafe.Application.Services.RestaurantGroup.Concrete;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using ECafe.Infrastructure.Authorization;
using ECafe.Infrastructure.Context;
using ECafe.Infrastructure.Repositories.RestaurantGroup;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace ECafe.Tests;

public sealed class TenantAdministrationBoundaryTests
{
    [Fact]
    public void GlobalAdministrationEndpointsRequirePlatformRoleAndManagementPermission()
    {
        var delete = typeof(UserController).GetMethod(nameof(UserController.Delete))!;
        Assert.Equal("1", Assert.Single(delete.GetCustomAttributes<AuthorizeAttribute>(), value => value.Roles != null).Roles);
        Assert.Contains(delete.GetCustomAttributes<HasPermissionAttribute>(),
            value => value.Policy == $"Permission:{(int)PermissionCode.ManageUsers}");

        Assert.Equal("1", Assert.Single(typeof(RestaurantGroupController).GetCustomAttributes<AuthorizeAttribute>()).Roles);
        foreach (var action in new[] { nameof(RestaurantGroupController.GetAll), nameof(RestaurantGroupController.Create) })
            Assert.Contains(typeof(RestaurantGroupController).GetMethod(action)!.GetCustomAttributes<HasPermissionAttribute>(),
                value => value.Policy == $"Permission:{(int)PermissionCode.ManageRestaurants}");
    }

    [Theory]
    [InlineData(RoleCode.Customer)]
    [InlineData(RoleCode.Owner)]
    [InlineData(RoleCode.Manager)]
    [InlineData(RoleCode.Waiter)]
    [InlineData(RoleCode.Kitchen)]
    public async Task NonAdminCannotEnumerateOrCreateGroupsEvenWithAnotherRestaurantSelected(RoleCode role)
    {
        await using var db = Database();
        db.RestaurantGroups.Add(new() { Id = 1, Name = "Private group", LegalName = "Private legal name", Email = "private@example.test", IsActive = true });
        await db.SaveChangesAsync();
        var service = Service(db, role);

        Assert.Equal(ErrorCode.AccessDenied, (await Assert.ThrowsAsync<ForbiddenException>(() => service.GetAllAsync())).Code);
        await Assert.ThrowsAsync<ForbiddenException>(() => service.CreateAsync(new() { Name = "Other", Email = "other@example.test" }));
        Assert.Equal(1, await db.RestaurantGroups.CountAsync());
    }

    [Fact]
    public async Task PlatformAdminCanListActiveGroupsAndCreateAGroup()
    {
        await using var db = Database();
        db.RestaurantGroups.AddRange(
            new RestaurantGroup { Id = 1, Name = "Active", LegalName = "Legal", Email = "active@example.test", IsActive = true },
            new RestaurantGroup { Id = 2, Name = "Inactive", Email = "inactive@example.test", IsActive = false });
        await db.SaveChangesAsync();
        var service = Service(db, RoleCode.SuperAdmin);
        var group = Assert.Single(await service.GetAllAsync());
        Assert.Equal("Legal", group.LegalName);
        Assert.Equal("active@example.test", group.Email);
        var id = await service.CreateAsync(new() { Name = "Created", Email = "created@example.test" });
        Assert.True(id > 0);
        Assert.Equal(2, (await service.GetAllAsync()).Count);
    }

    /// <summary>Test məlumatını yalnız yaddaş bazasında saxlayır; server bağlantısı yaratmır.</summary>
    private static ECafeDbContext Database() => new(new DbContextOptionsBuilder<ECafeDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    /// <summary>İstifadəçinin başqa restoran seçməsinin qlobal idarəetmə hüququ yaratmadığını sınamaq üçün xidmət qurur.</summary>
    private static RestaurantGroupManager Service(ECafeDbContext db, RoleCode role)
    {
        var http = new DefaultHttpContext { User = new(new ClaimsIdentity([
            new("userId", "5"), new(ClaimTypes.Role, ((int)role).ToString()),
            new("restaurantIds", "1,2"), new("restaurantRoles", "1:3,2:3")], "test")) };
        http.Request.Headers["X-Active-Restaurant-Id"] = "2";
        return new(new HttpContextAccessor { HttpContext = http }, Mock.Of<AutoMapper.IMapper>(),
            new ConfigurationBuilder().Build(), new RestaurantGroupRepository(db));
    }
}
