using ECafe.Api.Controllers;
using ECafe.Api.Swagger;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;

namespace ECafe.Tests;

public sealed class MobileAppSwaggerMetadataTests
{
    [Fact]
    public void EveryMobileAppEndpointHasSwaggerDescription()
    {
        Assert.True(EcafeSwaggerMetadata.Tags.ContainsKey("MobileApp"));

        var actions = typeof(MobileAppController).GetMethods()
            .Where(method => method.DeclaringType == typeof(MobileAppController))
            .Where(method => method.GetCustomAttributes(typeof(HttpMethodAttribute), true).Length > 0)
            .ToArray();

        Assert.Equal(11, actions.Length);
        foreach (var action in actions)
        {
            Assert.True(EcafeSwaggerMetadata.Endpoints.TryGetValue($"MobileApp.{action.Name}", out var metadata),
                $"Swagger metadata is missing for MobileApp.{action.Name}.");
            Assert.False(string.IsNullOrWhiteSpace(metadata!.Summary));
            Assert.False(string.IsNullOrWhiteSpace(metadata.Description));
        }
    }

    [Fact]
    public void StaffReleaseAndDownloadRequireAuthorization()
    {
        foreach (var name in new[] { "GetStaffAccess", "GetStaffRelease", "DownloadStaffRelease" })
        {
            var method = typeof(MobileAppController).GetMethod(name)!;
            Assert.NotNull(method.GetCustomAttributes(typeof(AuthorizeAttribute), true).SingleOrDefault());
            Assert.Empty(method.GetCustomAttributes(typeof(AllowAnonymousAttribute), true));
        }
    }
}
