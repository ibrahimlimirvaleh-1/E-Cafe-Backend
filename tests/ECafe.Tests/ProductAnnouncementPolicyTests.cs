using System.Reflection;
using ECafe.Api.Controllers;
using ECafe.Api.Security;
using ECafe.Domain.Enums;
using ECafe.Domain.Policies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Http.Metadata;
using Xunit;

namespace ECafe.Tests;

public sealed class ProductAnnouncementPolicyTests
{
    [Theory]
    [InlineData(1, "AnnouncementAdmin")]
    [InlineData(2, "DashboardReports")]
    [InlineData(3, "Inventory")]
    [InlineData(4, "WaiterTables")]
    [InlineData(5, "CustomerOrders")]
    [InlineData(6, "KitchenOrders")]
    public void RegisteredDestinationsRespectRoleAndFreshPermission(int role, string expected)
    {
        var permissions = Enum.GetValues<PermissionCode>().Select(value => (int)value).ToArray();
        Assert.Contains(expected, ProductAnnouncementPolicy.AllowedDestinations(role, permissions, false));
        Assert.All(ProductAnnouncementPolicy.AllowedDestinations(role, permissions, false),
            key => Assert.Contains(role, ProductAnnouncementPolicy.FindDestination(key)!.RoleIds));
    }

    [Theory]
    [InlineData(1, "Inventory")]
    [InlineData(2, "DashboardReports")]
    [InlineData(3, "Inventory")]
    [InlineData(4, "WaiterTables")]
    [InlineData(6, "KitchenOrders")]
    public void MissingPermissionSuppressesTargetEvenForAdmin(int role, string key)
        => Assert.DoesNotContain(key, ProductAnnouncementPolicy.AllowedDestinations(role, Array.Empty<int>(), false));

    [Theory]
    [InlineData("https://evil.example")]
    [InlineData("javascript:alert(1)")]
    [InlineData("//evil.example")]
    [InlineData("CustomerOrders/../../admin")]
    [InlineData("customerorders")]
    [InlineData(null)]
    public void DestinationIsClosedKeyNotRawNavigation(string? key)
        => Assert.Null(ProductAnnouncementPolicy.FindDestination(key));

    [Fact]
    public void DeletionAnnouncementIsOnlyForPureCustomer()
    {
        Assert.Contains(ProductAnnouncementPolicy.CustomerProfile, ProductAnnouncementPolicy.AllowedDestinations(5, Array.Empty<int>(), false));
        Assert.DoesNotContain(ProductAnnouncementPolicy.CustomerProfile, ProductAnnouncementPolicy.AllowedDestinations(5, Array.Empty<int>(), true));
        Assert.Empty(ProductAnnouncementPolicy.AllowedDestinations(99, Array.Empty<int>(), false));
    }

    [Fact]
    public void EndpointsRequireAuthenticationAdminRoleRateLimitAndBoundedJson()
    {
        var admin = typeof(AdminProductAnnouncementController);
        Assert.Equal("1", admin.GetCustomAttribute<AuthorizeAttribute>()!.Roles);
        Assert.Equal(16 * 1024, ((IRequestSizeLimitMetadata)admin.GetCustomAttribute<RequestSizeLimitAttribute>()!).MaxRequestBodySize);
        foreach (var type in new[] { admin, typeof(ProductAnnouncementController) })
        {
            Assert.NotNull(type.GetCustomAttribute<AuthorizeAttribute>());
            Assert.True(type.GetCustomAttribute<ResponseCacheAttribute>()!.NoStore);
            Assert.Equal(RateLimitPolicyNames.ProductAnnouncements, type.GetCustomAttribute<EnableRateLimitingAttribute>()!.PolicyName);
        }
        var ack = typeof(ProductAnnouncementController).GetMethod(nameof(ProductAnnouncementController.Acknowledge))!;
        Assert.Equal(1024, ((IRequestSizeLimitMetadata)ack.GetCustomAttribute<RequestSizeLimitAttribute>()!).MaxRequestBodySize);
        Assert.Contains("application/json", ack.GetCustomAttribute<ConsumesAttribute>()!.ContentTypes);
    }
}
