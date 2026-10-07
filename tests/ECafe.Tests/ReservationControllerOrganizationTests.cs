using System.Reflection;
using ECafe.Api.Controllers;
using ECafe.Application.DTOs.Reservation;
using ECafe.Application.Features.Commands.Reservation.Cancel;
using ECafe.Application.Features.Commands.Reservation.SubmitPaymentProof;
using ECafe.Application.Features.Commands.Reservation.SubmitRefundTransfer;
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

public sealed class ReservationControllerOrganizationTests
{
    [Theory]
    [InlineData(typeof(ReservationController), "GetMy,GetById,GetHistory,Create")]
    [InlineData(typeof(RestaurantReservationController), "GetList,GetService,GetById,GetHistory")]
    [InlineData(typeof(ReservationFlowController), "SubmitPaymentProof,Cancel,WaiveDeposit,SendPaymentInstruction,ApprovePaymentProof,RejectPaymentProof,MarkArrived,CheckIn,Complete,CancelForRestaurant")]
    [InlineData(typeof(ReservationRefundController), "GetRefund,GetRestaurantRefund,GetRefundPayoutDetails")]
    [InlineData(typeof(ReservationRefundFlowController), "RequestRefund,SubmitRefundPayoutDetails,ConfirmRefundTransfer,DisputeRefundTransfer,SubmitRefundTransfer")]
    [InlineData(typeof(ReservationArrivalController), "Get")]
    [InlineData(typeof(ReservationArrivalFlowController), "Offer,Accept")]
    public void ActionsAreGroupedByBusinessResponsibility(Type controller, string expectedActions)
    {
        var actual = GetActions(controller).Select(m => m.Name).OrderBy(n => n).ToArray();
        Assert.Equal(expectedActions.Split(',').OrderBy(n => n).ToArray(), actual);
        Assert.NotEmpty(controller.GetCustomAttributes<AuthorizeAttribute>());
        Assert.Empty(controller.GetCustomAttributes<AllowAnonymousAttribute>());
    }

    [Theory]
    [InlineData(typeof(ReservationFlowController), "POST")]
    [InlineData(typeof(ReservationRefundFlowController), "POST")]
    [InlineData(typeof(ReservationRefundController), "GET")]
    [InlineData(typeof(RestaurantReservationController), "GET")]
    [InlineData(typeof(ReservationArrivalController), "GET")]
    [InlineData(typeof(ReservationArrivalFlowController), "POST")]
    public void FlowCommandsAndReadQueriesUseSeparateControllers(Type controller, string expectedVerb)
        => Assert.All(GetActions(controller), method =>
            Assert.All(method.GetCustomAttributes<HttpMethodAttribute>(), attribute =>
                Assert.Equal(new[] { expectedVerb }, attribute.HttpMethods)));

    [Theory]
    [InlineData(typeof(ReservationController), nameof(ReservationController.Create))]
    [InlineData(typeof(ReservationController), nameof(ReservationController.GetMy))]
    [InlineData(typeof(ReservationController), nameof(ReservationController.GetById))]
    [InlineData(typeof(ReservationController), nameof(ReservationController.GetHistory))]
    [InlineData(typeof(ReservationFlowController), nameof(ReservationFlowController.Cancel))]
    [InlineData(typeof(ReservationFlowController), nameof(ReservationFlowController.SubmitPaymentProof))]
    [InlineData(typeof(ReservationRefundController), nameof(ReservationRefundController.GetRefund))]
    [InlineData(typeof(ReservationRefundFlowController), nameof(ReservationRefundFlowController.RequestRefund))]
    [InlineData(typeof(ReservationRefundFlowController), nameof(ReservationRefundFlowController.SubmitRefundPayoutDetails))]
    [InlineData(typeof(ReservationRefundFlowController), nameof(ReservationRefundFlowController.ConfirmRefundTransfer))]
    [InlineData(typeof(ReservationRefundFlowController), nameof(ReservationRefundFlowController.DisputeRefundTransfer))]
    [InlineData(typeof(ReservationArrivalController), nameof(ReservationArrivalController.Get))]
    [InlineData(typeof(ReservationArrivalFlowController), nameof(ReservationArrivalFlowController.Offer))]
    [InlineData(typeof(ReservationArrivalFlowController), nameof(ReservationArrivalFlowController.Accept))]
    public void CustomerEndpointsStillRequireCustomerRole(Type controller, string action)
    {
        var method = controller.GetMethod(action)!;
        var rules = controller.GetCustomAttributes<AuthorizeAttribute>()
            .Concat(method.GetCustomAttributes<AuthorizeAttribute>()).ToArray();
        Assert.Contains(rules, rule => rule.Roles == ((int)RoleCode.Customer).ToString());
        Assert.DoesNotContain(rules, rule => !string.IsNullOrEmpty(rule.Policy));
        Assert.Empty(method.GetCustomAttributes<AllowAnonymousAttribute>());
    }

    [Theory]
    [InlineData(typeof(RestaurantReservationController), nameof(RestaurantReservationController.GetList))]
    [InlineData(typeof(RestaurantReservationController), nameof(RestaurantReservationController.GetById))]
    [InlineData(typeof(RestaurantReservationController), nameof(RestaurantReservationController.GetHistory))]
    [InlineData(typeof(ReservationFlowController), nameof(ReservationFlowController.WaiveDeposit))]
    [InlineData(typeof(ReservationFlowController), nameof(ReservationFlowController.SendPaymentInstruction))]
    [InlineData(typeof(ReservationFlowController), nameof(ReservationFlowController.ApprovePaymentProof))]
    [InlineData(typeof(ReservationFlowController), nameof(ReservationFlowController.RejectPaymentProof))]
    [InlineData(typeof(ReservationFlowController), nameof(ReservationFlowController.Complete))]
    [InlineData(typeof(ReservationFlowController), nameof(ReservationFlowController.CancelForRestaurant))]
    [InlineData(typeof(ReservationRefundController), nameof(ReservationRefundController.GetRestaurantRefund))]
    [InlineData(typeof(ReservationRefundController), nameof(ReservationRefundController.GetRefundPayoutDetails))]
    [InlineData(typeof(ReservationRefundFlowController), nameof(ReservationRefundFlowController.SubmitRefundTransfer))]
    public void RestaurantEndpointsStillRequireReservationPermission(Type controller, string action)
    {
        var method = controller.GetMethod(action)!;
        var rules = controller.GetCustomAttributes<AuthorizeAttribute>()
            .Concat(method.GetCustomAttributes<AuthorizeAttribute>()).ToArray();
        Assert.Contains(rules, rule => rule.Policy == $"Permission:{(int)PermissionCode.ManageReservations}");
        Assert.DoesNotContain(rules, rule => !string.IsNullOrEmpty(rule.Roles));
        Assert.Empty(method.GetCustomAttributes<AllowAnonymousAttribute>());
    }

    [Theory]
    [InlineData(nameof(ReservationFlowController.MarkArrived), PermissionCode.RecordReservationArrival)]
    [InlineData(nameof(ReservationFlowController.CheckIn), PermissionCode.SeatReservationGuest)]
    public void ArrivalAndSeatingHaveSeparatePermissions(string action, PermissionCode permission)
    {
        var method = typeof(ReservationFlowController).GetMethod(action)!;
        var rules = method.GetCustomAttributes<AuthorizeAttribute>().ToArray();
        Assert.Contains(rules, rule => rule.Policy == $"Permission:{(int)permission}");
        Assert.DoesNotContain(rules, rule => rule.Policy == $"Permission:{(int)PermissionCode.ManageReservations}");
    }

    [Fact]
    public void ServiceListRequiresArrivalPermissionAndOmitsPaymentFields()
    {
        var method = typeof(RestaurantReservationController).GetMethod(
            nameof(RestaurantReservationController.GetService))!;
        Assert.Contains(method.GetCustomAttributes<AuthorizeAttribute>(), rule =>
            rule.Policy == $"Permission:{(int)PermissionCode.RecordReservationArrival}");

        var fields = typeof(ReservationServiceItemResponse).GetProperties()
            .Select(property => property.Name).ToArray();
        Assert.DoesNotContain(fields, name => name.Contains("Payment", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(fields, name => name.Contains("Deposit", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(fields, name => name.Contains("Refund", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(typeof(ReservationFlowController), nameof(ReservationFlowController.SubmitPaymentProof))]
    [InlineData(typeof(ReservationRefundFlowController), nameof(ReservationRefundFlowController.SubmitRefundTransfer))]
    public void FileSubmissionStillBindsMultipartForms(Type controller, string action)
    {
        var method = controller.GetMethod(action)!;
        var consumes = Assert.Single(method.GetCustomAttributes<ConsumesAttribute>());
        Assert.Equal(new[] { "multipart/form-data" }, consumes.ContentTypes);
        Assert.Single(method.GetParameters(), p => p.GetCustomAttribute<FromFormAttribute>() != null);
    }

    [Fact]
    public async Task CustomerCancellationStillForwardsIdReasonAndCancellationToken()
    {
        using var cancellation = new CancellationTokenSource();
        var mediator = new Mock<IMediator>();
        var response = new ReservationActionResponse { ReservationId = 41 };
        mediator.Setup(m => m.Send(It.IsAny<CancelReservationCommand>(), cancellation.Token)).ReturnsAsync(response);
        using var services = CreateServices(mediator.Object);
        var controller = AttachContext(new ReservationFlowController(), services);

        var result = Assert.IsType<OkObjectResult>(await controller.Cancel(
            41, new ReservationCancellationRequest { Reason = "Changed plans" }, cancellation.Token));

        Assert.Same(response, result.Value);
        mediator.Verify(m => m.Send(It.Is<CancelReservationCommand>(c =>
            c.ReservationId == 41 && c.Reason == "Changed plans"), cancellation.Token), Times.Once);
    }

    [Fact]
    public async Task PaymentProofRouteIdsOverrideFormIdsAndKeepCreatedResponse()
    {
        var mediator = new Mock<IMediator>();
        var response = new PaymentProofResponse();
        mediator.Setup(m => m.Send(It.IsAny<SubmitPaymentProofCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);
        using var services = CreateServices(mediator.Object);
        var controller = AttachContext(new ReservationFlowController(), services);
        var command = new SubmitPaymentProofCommand { RestaurantId = 999, ReservationId = 999 };

        var result = Assert.IsType<ObjectResult>(await controller.SubmitPaymentProof(2, 41, command, CancellationToken.None));

        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);
        Assert.Same(response, result.Value);
        mediator.Verify(m => m.Send(It.Is<SubmitPaymentProofCommand>(c =>
            c == command && c.RestaurantId == 2 && c.ReservationId == 41), CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task RefundTransferRouteIdsOverrideFormIdsAndKeepCreatedResponse()
    {
        var mediator = new Mock<IMediator>();
        var response = new ReservationRefundTransferResponse();
        mediator.Setup(m => m.Send(It.IsAny<SubmitReservationRefundTransferCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);
        using var services = CreateServices(mediator.Object);
        var controller = AttachContext(new ReservationRefundFlowController(), services);
        var command = new SubmitReservationRefundTransferCommand { RestaurantId = 999, RefundId = 999, TransferReference = "reference" };

        var result = Assert.IsType<ObjectResult>(await controller.SubmitRefundTransfer(2, 7, command, CancellationToken.None));

        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);
        Assert.Same(response, result.Value);
        mediator.Verify(m => m.Send(It.Is<SubmitReservationRefundTransferCommand>(c =>
            c == command && c.RestaurantId == 2 && c.RefundId == 7 && c.TransferReference == "reference"),
            CancellationToken.None), Times.Once);
    }

    private static IEnumerable<MethodInfo> GetActions(Type controller)
        => controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => method.GetCustomAttributes<HttpMethodAttribute>().Any());

    private static ServiceProvider CreateServices(IMediator mediator)
        => new ServiceCollection().AddSingleton(mediator).BuildServiceProvider();

    private static T AttachContext<T>(T controller, IServiceProvider services) where T : BaseController
    {
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { RequestServices = services }
        };
        return controller;
    }
}
