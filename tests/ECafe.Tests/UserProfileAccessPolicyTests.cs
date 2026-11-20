using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using AutoMapper;
using ECafe.Application.Services.Jwt.Concrete;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Domain.Policies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ECafe.Tests;

public sealed class UserProfileAccessPolicyTests
{
    [Theory]
    [InlineData(RoleCode.Owner)]
    [InlineData(RoleCode.Manager)]
    [InlineData(RoleCode.Waiter)]
    [InlineData(RoleCode.Kitchen)]
    public void StaffWithoutActiveProfilesGetsPersonalRoleWithoutFormerStaffPermissions(RoleCode role)
    {
        var user = Account(role);
        user.UserRestaurants.Add(new UserRestaurant { RestaurantId = 1, RoleId = (int)role, IsActive = false });
        var claims = Claims(user);
        Assert.Equal(((int)RoleCode.Customer).ToString(), Assert.Single(claims, value => value.Type == ClaimTypes.Role).Value);
        Assert.DoesNotContain(claims, value => value.Type is "permission" or "restaurantId" or "restaurantIds" or "restaurantRoles");
        Assert.Equal((int)role, user.RoleId);
        Assert.True(user.IsActive);
        Assert.Equal(7, user.SessionVersion);
        Assert.True(UserProfileAccessPolicy.PersonalAccounts.Compile()(user));
        user.IsActive = false;
        Assert.False(UserProfileAccessPolicy.PersonalAccounts.Compile()(user));
    }

    [Theory]
    [InlineData(RoleCode.SuperAdmin)]
    [InlineData(RoleCode.Customer)]
    public void AccountRolesDoNotRequireRestaurantProfiles(RoleCode role)
    {
        var user = Account(role);
        Assert.Equal((int)role, UserProfileAccessPolicy.ResolveAccountRole((int)role, false));
        Assert.Equal(((int)role).ToString(), Assert.Single(Claims(user), value => value.Type == ClaimTypes.Role).Value);
        Assert.Equal(role == RoleCode.Customer, UserProfileAccessPolicy.PersonalAccounts.Compile()(user));
    }

    [Fact]
    public void RemainingRestaurantProfilePreservesItsRoleAndOmitsRevokedProfile()
    {
        var user = Account(RoleCode.Owner);
        user.UserRestaurants.Add(new UserRestaurant { RestaurantId = 1, RoleId = (int)RoleCode.Owner, IsActive = false });
        user.UserRestaurants.Add(new UserRestaurant { RestaurantId = 2, RoleId = (int)RoleCode.Manager, IsActive = true });
        var claims = Claims(user);
        Assert.Equal("2:3", Assert.Single(claims, value => value.Type == "restaurantRoles").Value);
        Assert.Equal("2", Assert.Single(claims, value => value.Type == "restaurantIds").Value);
        Assert.Equal(((int)RoleCode.Owner).ToString(), Assert.Single(claims, value => value.Type == ClaimTypes.Role).Value);
        Assert.Contains(claims, value => value.Type == "permission" && value.Value == nameof(PermissionCode.ManageStaff));
        Assert.False(UserProfileAccessPolicy.PersonalAccounts.Compile()(user));
    }

    /// <summary>Köhnə personal icazəsi olan sintetik hesab yaradır ki, müştəri tokeninə sızma açıq yoxlanılsın.</summary>
    private static User Account(RoleCode role) => new()
    {
        Id = 5, Name = "Test", Surname = "Account", Email = "profile@example.test", IsActive = true,
        RoleId = (int)role, SessionVersion = 7,
        Role = new Role { Id = (int)role, RolePermissions = [new RolePermission
        {
            PermissionId = (int)PermissionCode.ManageStaff,
            Permission = new Permission { Id = (int)PermissionCode.ManageStaff, Name = "Manage staff" }
        }] }
    };

    /// <summary>Real JWT generator-u yalnız sintetik imza açarı ilə işlədir və nəticəni loglamadan oxuyur.</summary>
    private static List<Claim> Claims(User user)
    {
        var mapper = new MapperConfiguration(_ => { }, NullLoggerFactory.Instance).CreateMapper();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Key"] = "profile-policy-tests-only-0123456789012345678901234567890123456789",
            ["Jwt:Issuer"] = "profile-test", ["Jwt:Audience"] = "profile-test"
        }).Build();
        var token = new JwtManager(new HttpContextAccessor(), mapper, config).GenerateToken(user, sessionId: "test-device");
        return new JwtSecurityTokenHandler().ReadJwtToken(token).Claims.ToList();
    }
}
