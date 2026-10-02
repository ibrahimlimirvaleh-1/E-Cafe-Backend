using System.Reflection;
using ECafe.Api.Controllers;
using ECafe.Application.DTOs.Reservation;
using ECafe.Application.Features.Commands.RestaurantContract;
using ECafe.Application.Services.Reservation.Abstract;
using ECafe.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace ECafe.Tests;

public sealed class PreparedFlowControllerTests
{
    [Fact]
    public void ContractFlowContainsOnlyLifecycleCommands()
    {
        var actions = GetActions(typeof(RestaurantContractFlowController)).ToArray();
        Assert.Equal(new[] { "Activate", "Approve", "SendForSignature", "Terminate" },
            actions.Select(m => m.Name).OrderBy(n => n).ToArray());
        Assert.All(actions, action =>
            Assert.Equal(new[] { "POST" }, Assert.Single(action.GetCustomAttributes<HttpMethodAttribute>()).HttpMethods));
        Assert.NotEmpty(typeof(RestaurantContractFlowController).GetCustomAttributes<AuthorizeAttribute>());
    }

    [Fact]
    public void ContractResourceKeepsCreationEditingAndReadQueries()
    {
        var actions = GetActions(typeof(RestaurantContractController)).ToArray();
        Assert.Equal(new[] { "Create", "GetActions", "GetActive", "GetByRestaurant", "GetPagedByRestaurant", "Update" },
            actions.Select(m => m.Name).OrderBy(n => n).ToArray());
    }

    [Theory]
    [InlineData(typeof(RestaurantContractFlowController), nameof(RestaurantContractFlowController.SendForSignature), PermissionCode.ManageRestaurantContracts)]
    [InlineData(typeof(RestaurantContractFlowController), nameof(RestaurantContractFlowController.Activate), PermissionCode.ManageRestaurantContracts)]
    [InlineData(typeof(RestaurantContractFlowController), nameof(RestaurantContractFlowController.Terminate), PermissionCode.ManageRestaurantContracts)]
    [InlineData(typeof(RestaurantContractFlowController), nameof(RestaurantContractFlowController.Approve), PermissionCode.ViewRestaurantContracts)]
    [InlineData(typeof(RestaurantContractController), nameof(RestaurantContractController.Create), PermissionCode.ManageRestaurantContracts)]
    [InlineData(typeof(RestaurantContractController), nameof(RestaurantContractController.Update), PermissionCode.ManageRestaurantContracts)]
    [InlineData(typeof(RestaurantContractController), nameof(RestaurantContractController.GetByRestaurant), PermissionCode.ViewRestaurantContracts)]
    [InlineData(typeof(RestaurantContractController), nameof(RestaurantContractController.GetPagedByRestaurant), PermissionCode.ViewRestaurantContracts)]
    [InlineData(typeof(RestaurantContractController), nameof(RestaurantContractController.GetActive), PermissionCode.ViewRestaurantContracts)]
    [InlineData(typeof(RestaurantContractController), nameof(RestaurantContractController.GetActions), PermissionCode.ViewRestaurantContracts)]
    public void ContractPermissionsArePreserved(Type controller, string action, PermissionCode permission)
    {
        var method = controller.GetMethod(action)!;
        var rule = Assert.Single(method.GetCustomAttributes<AuthorizeAttribute>());
        Assert.Equal($"Permission:{(int)permission}", rule.Policy);
        Assert.Empty(method.GetCustomAttributes<AllowAnonymousAttribute>());
        Assert.Empty(controller.GetCustomAttributes<AllowAnonymousAttribute>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ContractApprovalPreservesOptionalBodyAndRouteOwnership(bool hasBody)
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<ApproveRestaurantContractCommand>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        using var services = new ServiceCollection().AddSingleton(mediator.Object).BuildServiceProvider();
        var controller = new RestaurantContractFlowController
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { RequestServices = services }
            }
        };
        var command = hasBody ? new ApproveRestaurantContractCommand
        {
            RestaurantId = 999, ContractId = 999,
            HasAcceptedContractTerms = true, AcceptanceText = "Accepted terms"
        } : null;

        Assert.IsType<OkResult>(await controller.Approve(2, 7, command));

        mediator.Verify(m => m.Send(It.Is<ApproveRestaurantContractCommand>(c =>
            c.RestaurantId == 2 && c.ContractId == 7 &&
            c.HasAcceptedContractTerms == hasBody && c.AcceptanceText == (hasBody ? "Accepted terms" : null)),
            CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task ArrivalOfferStillForwardsRequestedArrivalAndCancellationToken()
    {
        using var cancellation = new CancellationTokenSource();
        var arrivalAt = new DateTimeOffset(2026, 10, 3, 15, 30, 0, TimeSpan.FromHours(4));
        var service = new Mock<IReservationArrivalService>();
        var response = CreateArrivalResponse(arrivalAt);
        service.Setup(s => s.OfferAsync(41, arrivalAt, cancellation.Token)).ReturnsAsync(response);
        var controller = new ReservationArrivalFlowController(service.Object);

        var result = Assert.IsType<OkObjectResult>(await controller.Offer(
            41, new ReservationArrivalOfferRequest(arrivalAt), cancellation.Token));

        Assert.Same(response, result.Value);
        service.Verify(s => s.OfferAsync(41, arrivalAt, cancellation.Token), Times.Once);
    }

    [Fact]
    public async Task ArrivalAcceptanceStillForwardsConsentToken()
    {
        using var cancellation = new CancellationTokenSource();
        var consent = Guid.NewGuid();
        var service = new Mock<IReservationArrivalService>();
        var response = CreateArrivalResponse(DateTimeOffset.UtcNow) with { ConsentToken = consent, Accepted = true };
        service.Setup(s => s.AcceptAsync(41, consent, cancellation.Token)).ReturnsAsync(response);
        var controller = new ReservationArrivalFlowController(service.Object);

        var result = Assert.IsType<OkObjectResult>(await controller.Accept(
            41, new AcceptReservationArrivalRequest(consent), cancellation.Token));

        Assert.Same(response, result.Value);
        service.Verify(s => s.AcceptAsync(41, consent, cancellation.Token), Times.Once);
    }

    private static ReservationArrivalOfferResponse CreateArrivalResponse(DateTimeOffset arrivalAt)
        => new(Guid.NewGuid(), arrivalAt, arrivalAt.AddMinutes(15), null,
            arrivalAt.AddMinutes(3), false, false, false, "Asia/Baku");

    private static IEnumerable<MethodInfo> GetActions(Type controller)
        => controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => method.GetCustomAttributes<HttpMethodAttribute>().Any());
}
