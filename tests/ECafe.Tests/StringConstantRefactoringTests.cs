using System.IdentityModel.Tokens.Jwt;
using System.Reflection;
using System.Security.Claims;
using ECafe.Api.Controllers;
using ECafe.Application.Common.Configuration;
using ECafe.Application.Services.Jwt.Concrete;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Domain.Workflow;
using ECafe.Infrastructure.Context;
using ECafe.Shared.Extensions;
using ECafe.Shared.Http;
using ECafe.Shared.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ECafe.Tests;

public sealed class StringConstantRefactoringTests
{
    [Theory]
    [InlineData(AuthenticationClaimTypes.UserId, "userId")]
    [InlineData(AuthenticationClaimTypes.Name, "name")]
    [InlineData(AuthenticationClaimTypes.Surname, "surname")]
    [InlineData(AuthenticationClaimTypes.Email, "email")]
    [InlineData(AuthenticationClaimTypes.IsActive, "isActive")]
    [InlineData(AuthenticationClaimTypes.SessionVersion, "sessionVersion")]
    [InlineData(AuthenticationClaimTypes.SessionId, "sessionId")]
    [InlineData(AuthenticationClaimTypes.FileUrl, "fileUrl")]
    [InlineData(AuthenticationClaimTypes.RestaurantId, "restaurantId")]
    [InlineData(AuthenticationClaimTypes.RestaurantIds, "restaurantIds")]
    [InlineData(AuthenticationClaimTypes.RestaurantRoles, "restaurantRoles")]
    [InlineData(AuthenticationClaimTypes.RoleName, "roleName")]
    [InlineData(AuthenticationClaimTypes.Permission, "permission")]
    public void JwtClaimNamesKeepTheirExactExistingWireValues(string actual, string expected)
        => Assert.Equal(expected, actual);

    [Theory]
    [InlineData(RequestHeaderNames.IdempotencyKey, "Idempotency-Key")]
    [InlineData(RequestHeaderNames.ActiveRestaurantId, "X-Active-Restaurant-Id")]
    [InlineData(RequestHeaderNames.ActiveRoleId, "X-Active-Role-Id")]
    [InlineData(RequestHeaderNames.CorrelationId, "X-Correlation-Id")]
    public void CustomHeaderNamesKeepExistingClientContract(string actual, string expected)
        => Assert.Equal(expected, actual);

    [Theory]
    [InlineData(AuthenticationConfigurationKeys.FrontendBaseUrl, "Frontend:BaseUrl")]
    [InlineData(AuthenticationConfigurationKeys.Jwt.Key, "Jwt:Key")]
    [InlineData(AuthenticationConfigurationKeys.Jwt.Issuer, "Jwt:Issuer")]
    [InlineData(AuthenticationConfigurationKeys.Jwt.Audience, "Jwt:Audience")]
    [InlineData(AuthenticationConfigurationKeys.Jwt.AccessTokenLifetimeMinutes, "Jwt:AccessTokenLifetimeMinutes")]
    public void ConfigurationPathsDoNotRequireEnvOrAppsettingsMigration(string actual, string expected)
        => Assert.Equal(expected, actual);

    [Theory]
    [InlineData(ContractActionCode.SendForSignature, "sendForSignature")]
    [InlineData(ContractActionCode.Approve, "approve")]
    [InlineData(ContractActionCode.Activate, "activate")]
    [InlineData(ContractActionCode.Terminate, "terminate")]
    [InlineData(ReservationActionCode.Complete, "complete")]
    public void WorkflowActionNamesKeepExistingStoredValues(string actual, string expected)
        => Assert.Equal(expected, actual);

    [Theory]
    [InlineData(typeof(CustomerOrderController), "Create")]
    [InlineData(typeof(OrderController), "Create")]
    [InlineData(typeof(PaymentFlowController), "CreateCheckout")]
    [InlineData(typeof(SessionBillController), "Checkout")]
    public void ExistingIdempotencyEndpointsKeepTheirHeaderBinding(Type controller, string action)
    {
        var method = controller.GetMethod(action);
        Assert.NotNull(method);
        var parameter = Assert.Single(method.GetParameters(), value => value.GetCustomAttribute<FromHeaderAttribute>() != null);
        Assert.Equal("Idempotency-Key", parameter.GetCustomAttribute<FromHeaderAttribute>()!.Name);
    }

    [Fact]
    public void JsonConsumersKeepTheirExactMediaTypeAfterUsingFrameworkConstants()
    {
        Type[] controllers = [typeof(AuthController), typeof(MobileAuthController), typeof(EmailChangeController),
            typeof(CustomerAccountDeletionController), typeof(ProductAnnouncementController), typeof(AdminProductAnnouncementController)];
        foreach (var controller in controllers)
        {
            var attributes = controller.GetCustomAttributes<ConsumesAttribute>()
                .Concat(controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .SelectMany(method => method.GetCustomAttributes<ConsumesAttribute>()));
            Assert.Contains(attributes, attribute => attribute.ContentTypes.Contains("application/json"));
        }
    }

    [Fact]
    public void LegacyLiteralClaimsAreStillReadWithoutNewClaimNames()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim("userId", "17"), new Claim("restaurantId", "2"), new Claim(ClaimTypes.Role, "3")], "test"));
        Assert.Equal(17, principal.GetUserId());
        Assert.Equal(2, principal.GetRestaurantId());
        Assert.Equal(3, principal.GetRoleId());
    }

    [Fact]
    public void JwtCreationStillEmitsLegacyNamesRolesAndSessionDataFromExistingConfigKeys()
    {
        var user = new User
        {
            Id = 17, Name = "Synthetic", Surname = "User", Email = "synthetic@example.test", IsActive = true,
            RoleId = (int)RoleCode.Manager, SessionVersion = 4,
            Role = new Role
            {
                Id = (int)RoleCode.Manager,
                RolePermissions = [new RolePermission
                {
                    PermissionId = (int)PermissionCode.ManageReservations,
                    Permission = new Permission { Id = (int)PermissionCode.ManageReservations }
                }]
            },
            UserRestaurants = [new UserRestaurant { RestaurantId = 9, RoleId = (int)RoleCode.Waiter, IsActive = true },
                new UserRestaurant { RestaurantId = 2, RoleId = (int)RoleCode.Manager, IsActive = true }]
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Key"] = new string('x', 64), ["Jwt:Issuer"] = "synthetic-issuer", ["Jwt:Audience"] = "synthetic-audience",
            ["Jwt:AccessTokenLifetimeMinutes"] = "3"
        }).Build();
        var service = new JwtManager(new HttpContextAccessor(), null!, configuration);
        var session = new string('a', 32);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(service.GenerateToken(user, "https://example.test/image", session));
        var claims = jwt.Claims.ToDictionary(claim => claim.Type, claim => claim.Value);
        Assert.Equal("17", claims["userId"]);
        Assert.Equal("Synthetic", claims["name"]);
        Assert.Equal("User", claims["surname"]);
        Assert.Equal(user.Email, claims["email"]);
        Assert.Equal("True", claims["isActive"]);
        Assert.Equal("4", claims["sessionVersion"]);
        Assert.Equal(session, claims["sessionId"]);
        Assert.Equal("https://example.test/image", claims["fileUrl"]);
        Assert.Equal("2", claims["restaurantId"]);
        Assert.Equal("2,9", claims["restaurantIds"]);
        Assert.Equal($"2:{(int)RoleCode.Manager},9:{(int)RoleCode.Waiter}", claims["restaurantRoles"]);
        Assert.Equal(nameof(PermissionCode.ManageReservations), claims["permission"]);
        Assert.Equal(((int)RoleCode.Manager).ToString(), claims[ClaimTypes.Role]);
        Assert.Equal("synthetic-issuer", jwt.Issuer);
        Assert.Equal("synthetic-audience", Assert.Single(jwt.Audiences));
        Assert.InRange((jwt.ValidTo - DateTime.UtcNow).TotalMinutes, 2, 3.1);
    }

    [Fact]
    public void ExistingWorkflowSeedValuesAndSchemaSnapshotRemainUnchanged()
    {
        using var db = new ECafeDbContext(new DbContextOptionsBuilder<ECafeDbContext>()
            .UseNpgsql("Host=127.0.0.1;Database=not_connected;Username=test;Password=test").Options);
        Assert.False(db.Database.HasPendingModelChanges());
        var rows = db.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(WorkflowActionRule))!.GetSeedData().ToList();
        var contract = rows.Where(row => (string)row[nameof(WorkflowActionRule.FlowCode)]! == WorkflowFlowCode.FromStatusType(ECafe.Domain.Enums.StatusType.Contract));
        foreach (var code in new[] { "sendForSignature", "approve", "activate", "terminate" })
            Assert.Contains(contract, row => (string)row[nameof(WorkflowActionRule.ActionCode)]! == code
                && (string)row[nameof(WorkflowActionRule.HttpMethod)]! == "POST");
        Assert.Contains(rows, row => (string)row[nameof(WorkflowActionRule.ActionCode)]! == "complete");
    }
}
