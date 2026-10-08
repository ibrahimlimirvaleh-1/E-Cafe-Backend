using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Domain.Workflow;
using Microsoft.EntityFrameworkCore;
using ApiRoutes = ECafe.Application.Routes.Routes;
using StatusTypeEnum = ECafe.Domain.Enums.StatusType;

namespace ECafe.Infrastructure.Seeders;

public static class WorkflowActionRuleSeeder
{
    private const string MarkArrivalLabel = "Müştəri gəlib";
    private const string SeatGuestLabel = "Masaya əyləşdir";

    private static string ReservationFlow
        => WorkflowFlowCode.FromStatusType(StatusTypeEnum.Reservation);

    private static string OrderFlow
        => WorkflowFlowCode.FromStatusType(StatusTypeEnum.Order);

    private static string KitchenFlow
        => WorkflowFlowCode.FromStatusType(StatusTypeEnum.Order);

    private static string PaymentFlow
        => WorkflowFlowCode.FromStatusType(StatusTypeEnum.PaymentStatus);

    private static string RefundFlow
        => WorkflowFlowCode.FromStatusType(StatusTypeEnum.Refund);

    public static void Seed(ModelBuilder modelBuilder)
    {
        var rules = new List<WorkflowActionRule>();
        var id = 1;

        AddContractRules(rules, ref id);
        AddReservationRules(rules, ref id);
        AddScheduledContractRules(rules, ref id);
        AddReceiptReservationRules(rules, ref id);
        AddOwnerReservationRules(rules, ref id);
        AddPaymentInstructionReservationRules(rules, ref id);
        AddReservationResponseRules(rules, ref id);
        AddReservationSessionRules(rules, ref id);
        AddReservationRefundRules(rules, ref id);
        AddRefundPayoutDetailsRules(rules, ref id);
        AddRefundTransferRules(rules, ref id);
        AddRefundCustomerReviewRules(rules, ref id);
        AddRefundPayoutViewRules(rules, ref id);
        AddReservationDepositWaiverRules(rules, ref id);
        AddReservationArrivalAndSeatingRules(rules, ref id);

        modelBuilder.Entity<WorkflowActionRule>().HasData(rules);
    }

    private static void AddContractRules(List<WorkflowActionRule> rules, ref int id)
    {
        var flowCode = WorkflowFlowCode.FromStatusType(StatusTypeEnum.Contract);
        rules.Add(Rule(id++, flowCode, StatusTypeEnum.Contract, ContractStatus.Draft, RoleCode.SuperAdmin, WorkflowActionCode.Contract.SendForSignature, "Sahibkar təsdiqinə göndər", "POST", WorkflowEndpoint(ApiRoutes.RestaurantContractFlow.SendForSignature), 10));
        rules.Add(Rule(id++, flowCode, StatusTypeEnum.Contract, ContractStatus.Draft, RoleCode.SuperAdmin, WorkflowActionCode.Contract.Terminate, "Müqaviləni ləğv et", "POST", WorkflowEndpoint(ApiRoutes.RestaurantContractFlow.Terminate), 90, true));
        rules.Add(Rule(id++, flowCode, StatusTypeEnum.Contract, ContractStatus.PendingSignature, RoleCode.Owner, WorkflowActionCode.Contract.Approve, "Müqaviləni təsdiqlə", "POST", WorkflowEndpoint(ApiRoutes.RestaurantContractFlow.Approve), 10, true));
        rules.Add(Rule(id++, flowCode, StatusTypeEnum.Contract, ContractStatus.PendingSignature, RoleCode.SuperAdmin, WorkflowActionCode.Contract.Terminate, "Müqaviləni ləğv et", "POST", WorkflowEndpoint(ApiRoutes.RestaurantContractFlow.Terminate), 90, true));
        rules.Add(Rule(id++, flowCode, StatusTypeEnum.Contract, ContractStatus.OwnerApproved, RoleCode.SuperAdmin, WorkflowActionCode.Contract.Activate, "Müqaviləni aktivləşdir", "POST", WorkflowEndpoint(ApiRoutes.RestaurantContractFlow.Activate), 10));
        rules.Add(Rule(id++, flowCode, StatusTypeEnum.Contract, ContractStatus.OwnerApproved, RoleCode.SuperAdmin, WorkflowActionCode.Contract.Terminate, "Müqaviləni ləğv et", "POST", WorkflowEndpoint(ApiRoutes.RestaurantContractFlow.Terminate), 90, true));
        rules.Add(Rule(id++, flowCode, StatusTypeEnum.Contract, ContractStatus.Active, RoleCode.SuperAdmin, WorkflowActionCode.Contract.Terminate, "Müqaviləni ləğv et", "POST", WorkflowEndpoint(ApiRoutes.RestaurantContractFlow.Terminate), 90, true));
    }

    private static void AddScheduledContractRules(List<WorkflowActionRule> rules, ref int id)
    {
        var flowCode = WorkflowFlowCode.FromStatusType(StatusTypeEnum.Contract);
        rules.Add(Rule(id++, flowCode, StatusTypeEnum.Contract, ContractStatus.Scheduled, RoleCode.SuperAdmin, WorkflowActionCode.Contract.Terminate, "Müqaviləni ləğv et", "POST", WorkflowEndpoint(ApiRoutes.RestaurantContractFlow.Terminate), 90, true));
    }

    private static void AddReservationRules(List<WorkflowActionRule> rules, ref int id)
    {
        var flowCode = WorkflowFlowCode.FromStatusType(StatusTypeEnum.Reservation);
        rules.Add(Rule(id++, flowCode, StatusTypeEnum.Reservation, ReservationStatus.PendingPayment, RoleCode.Customer, WorkflowActionCode.Reservation.Cancel, "Rezervasiyanı ləğv et", "POST", WorkflowEndpoint(ApiRoutes.ReservationFlow.Cancel), 90, true));
        rules.Add(Rule(id++, flowCode, StatusTypeEnum.Reservation, ReservationStatus.PendingPayment, RoleCode.Manager, WorkflowActionCode.Reservation.Cancel, "Rezervasiyanı ləğv et", "POST", WorkflowEndpoint(ApiRoutes.ReservationFlow.CancelForRestaurant), 90, true));
        rules.Add(Rule(id++, flowCode, StatusTypeEnum.Reservation, ReservationStatus.PendingPayment, RoleCode.SuperAdmin, WorkflowActionCode.Reservation.Cancel, "Rezervasiyanı ləğv et", "POST", WorkflowEndpoint(ApiRoutes.ReservationFlow.CancelForRestaurant), 90, true));
        rules.Add(Rule(id++, ReservationFlow, StatusTypeEnum.Reservation, ReservationStatus.Confirmed, RoleCode.Customer, WorkflowActionCode.Reservation.Cancel, "Rezervasiyanı ləğv et", "POST", WorkflowEndpoint(ApiRoutes.ReservationFlow.Cancel), 90, true));
    }

    private static void AddReservationSessionRules(List<WorkflowActionRule> rules, ref int id)
    {
        rules.Add(Rule(id++, ReservationFlow, StatusTypeEnum.Reservation, ReservationStatus.Confirmed, RoleCode.Manager, WorkflowActionCode.Reservation.MarkArrived, MarkArrivalLabel, "POST", WorkflowEndpoint(ApiRoutes.ReservationFlow.MarkArrived), 10));
        rules.Add(Rule(id++, ReservationFlow, StatusTypeEnum.Reservation, ReservationStatus.Confirmed, RoleCode.Waiter, WorkflowActionCode.Reservation.CheckIn, SeatGuestLabel, "POST", WorkflowEndpoint(ApiRoutes.ReservationFlow.CheckIn), 20));
        rules.Add(Rule(id++, ReservationFlow, StatusTypeEnum.Reservation, ReservationStatus.Seated, RoleCode.Manager, WorkflowActionCode.Reservation.Complete, "Masa sessiyasını bağla", "POST", WorkflowEndpoint(ApiRoutes.ReservationFlow.Complete), 10, true));
        rules.Add(Rule(id++, ReservationFlow, StatusTypeEnum.Reservation, ReservationStatus.Seated, RoleCode.Owner, WorkflowActionCode.Reservation.Complete, "Masa sessiyasını bağla", "POST", WorkflowEndpoint(ApiRoutes.ReservationFlow.Complete), 10, true));
    }

    private static void AddReservationRefundRules(List<WorkflowActionRule> rules, ref int id)
    {
        rules.Add(Rule(id++, ReservationFlow, StatusTypeEnum.Reservation, ReservationStatus.Confirmed, RoleCode.Manager, WorkflowActionCode.Reservation.Cancel, "Rezervasiyanı ləğv et", "POST", WorkflowEndpoint(ApiRoutes.ReservationFlow.CancelForRestaurant), 90, true));
        rules.Add(Rule(id++, ReservationFlow, StatusTypeEnum.Reservation, ReservationStatus.Confirmed, RoleCode.Owner, WorkflowActionCode.Reservation.Cancel, "Rezervasiyanı ləğv et", "POST", WorkflowEndpoint(ApiRoutes.ReservationFlow.CancelForRestaurant), 90, true));
        rules.Add(Rule(id++, ReservationFlow, StatusTypeEnum.Reservation, ReservationStatus.Cancelled, RoleCode.Customer, WorkflowActionCode.Reservation.RequestRefund, "Geri ödəniş soruş", "POST", WorkflowEndpoint(ApiRoutes.ReservationRefundFlow.RequestRefund), 100, true));
    }

    private static void AddRefundPayoutDetailsRules(List<WorkflowActionRule> rules, ref int id)
    {
        rules.Add(Rule(
            id++,
            RefundFlow,
            StatusTypeEnum.Refund,
            RefundStatus.AwaitingPayoutDetails,
            RoleCode.Customer,
            WorkflowActionCode.Refund.SubmitPayoutDetails,
            "Geri ödəniş məlumatını göndər",
            "POST",
            WorkflowEndpoint(ApiRoutes.ReservationRefundFlow.SubmitRefundPayoutDetails),
            10));
    }

    private static void AddRefundTransferRules(List<WorkflowActionRule> rules, ref int id)
    {
        var endpoint = WorkflowEndpoint(ApiRoutes.ReservationRefundFlow.SubmitRefundTransfer);

        AddRefundTransferRules(rules, ref id, RefundStatus.ReadyForPayout, endpoint);
        AddRefundTransferRules(rules, ref id, RefundStatus.Disputed, endpoint);
    }

    private static void AddRefundTransferRules(
        List<WorkflowActionRule> rules,
        ref int id,
        RefundStatus status,
        string endpoint)
    {
        rules.Add(Rule(
            id++,
            RefundFlow,
            StatusTypeEnum.Refund,
            status,
            RoleCode.Manager,
            WorkflowActionCode.Refund.SubmitTransfer,
            "Geri ödəniş çekini göndər",
            "POST",
            endpoint,
            10,
            requiresConfirmation: true));
        rules.Add(Rule(
            id++,
            RefundFlow,
            StatusTypeEnum.Refund,
            status,
            RoleCode.Owner,
            WorkflowActionCode.Refund.SubmitTransfer,
            "Geri ödəniş çekini göndər",
            "POST",
            endpoint,
            10,
            requiresConfirmation: true));
    }

    private static void AddRefundCustomerReviewRules(List<WorkflowActionRule> rules, ref int id)
    {
        rules.Add(Rule(
            id++, RefundFlow, StatusTypeEnum.Refund, RefundStatus.Processing,
            RoleCode.Customer, WorkflowActionCode.Refund.ConfirmTransfer,
            "Geri ödənişi təsdiqlə", "POST",
            WorkflowEndpoint(ApiRoutes.ReservationRefundFlow.ConfirmRefundTransfer), 10,
            requiresConfirmation: true));

        rules.Add(Rule(
            id++, RefundFlow, StatusTypeEnum.Refund, RefundStatus.Processing,
            RoleCode.Customer, WorkflowActionCode.Refund.DisputeTransfer,
            "Geri ödənişə etiraz et", "POST",
            WorkflowEndpoint(ApiRoutes.ReservationRefundFlow.DisputeRefundTransfer), 20,
            requiresConfirmation: true, requiresReason: true));
    }

    private static void AddRefundPayoutViewRules(List<WorkflowActionRule> rules, ref int id)
    {
        var endpoint = WorkflowEndpoint(ApiRoutes.ReservationRefund.GetRefundPayoutDetails);

        foreach (var status in new[] { RefundStatus.ReadyForPayout, RefundStatus.Processing, RefundStatus.Disputed })
        {
            foreach (var role in new[] { RoleCode.Manager, RoleCode.Owner })
            {
                rules.Add(Rule(
                    id++, RefundFlow, StatusTypeEnum.Refund, status, role,
                    WorkflowActionCode.Refund.ViewPayoutDetails,
                    "Ödəniş rekvizitinə bax", "GET", endpoint, 5));
            }
        }
    }

    private static void AddReceiptReservationRules(List<WorkflowActionRule> rules, ref int id)
    {
        rules.Add(Rule(id++, ReservationFlow, StatusTypeEnum.Reservation, ReservationStatus.PendingPayment, RoleCode.Customer, WorkflowActionCode.Reservation.SubmitPaymentProof, "Ödəniş çekini göndər", "POST", WorkflowEndpoint(ApiRoutes.ReservationFlow.SubmitPaymentProof), 10));
        rules.Add(Rule(id++, ReservationFlow, StatusTypeEnum.Reservation, ReservationStatus.PaymentSubmitted, RoleCode.Manager, WorkflowActionCode.Reservation.ApprovePaymentProof, "Ödənişi təsdiqlə", "POST", WorkflowEndpoint(ApiRoutes.ReservationFlow.ApprovePaymentProof), 10));
        rules.Add(Rule(id++, ReservationFlow, StatusTypeEnum.Reservation, ReservationStatus.PaymentSubmitted, RoleCode.Manager, WorkflowActionCode.Reservation.RejectPaymentProof, "Ödənişi rədd et", "POST", WorkflowEndpoint(ApiRoutes.ReservationFlow.RejectPaymentProof), 20, true, requiresReason: true));
        rules.Add(Rule(id++, ReservationFlow, StatusTypeEnum.Reservation, ReservationStatus.PaymentSubmitted, RoleCode.Manager, WorkflowActionCode.Reservation.Cancel, "Rezervasiyanı ləğv et", "POST", WorkflowEndpoint(ApiRoutes.ReservationFlow.CancelForRestaurant), 90, true));
    }

    private static void AddReservationDepositWaiverRules(List<WorkflowActionRule> rules, ref int id)
    {
        var endpoint = WorkflowEndpoint(ApiRoutes.ReservationFlow.WaiveDeposit);

        foreach (var role in new[] { RoleCode.Manager, RoleCode.Owner })
        {
            rules.Add(Rule(
                id++, ReservationFlow, StatusTypeEnum.Reservation,
                ReservationStatus.AwaitingPaymentInstruction, role,
                WorkflowActionCode.Reservation.WaiveDeposit,
                "Depozitdən imtina et", "POST", endpoint, 20,
                requiresConfirmation: true, requiresReason: true));
        }
    }

    private static void AddReservationArrivalAndSeatingRules(List<WorkflowActionRule> rules, ref int id)
    {
        var endpoint = WorkflowEndpoint(ApiRoutes.ReservationFlow.MarkArrived);
        foreach (var role in new[] { RoleCode.Owner, RoleCode.Waiter })
        {
            rules.Add(Rule(id++, ReservationFlow, StatusTypeEnum.Reservation,
                ReservationStatus.Confirmed, role, WorkflowActionCode.Reservation.MarkArrived,
                MarkArrivalLabel, "POST", endpoint, 10));
        }
    }

    private static string WorkflowEndpoint(string apiRoute)
        => "/" + apiRoute.Replace(":int", string.Empty, StringComparison.Ordinal);

    private static void AddOwnerReservationRules(List<WorkflowActionRule> rules, ref int id)
    {
        rules.Add(Rule(id++, ReservationFlow, StatusTypeEnum.Reservation, ReservationStatus.PendingPayment, RoleCode.Owner, WorkflowActionCode.Reservation.Cancel, "Rezervasiyanı ləğv et", "POST", WorkflowEndpoint(ApiRoutes.ReservationFlow.CancelForRestaurant), 90, true));
        rules.Add(Rule(id++, ReservationFlow, StatusTypeEnum.Reservation, ReservationStatus.PaymentSubmitted, RoleCode.Owner, WorkflowActionCode.Reservation.ApprovePaymentProof, "Ödənişi təsdiqlə", "POST", WorkflowEndpoint(ApiRoutes.ReservationFlow.ApprovePaymentProof), 10));
        rules.Add(Rule(id++, ReservationFlow, StatusTypeEnum.Reservation, ReservationStatus.PaymentSubmitted, RoleCode.Owner, WorkflowActionCode.Reservation.RejectPaymentProof, "Ödənişi rədd et", "POST", WorkflowEndpoint(ApiRoutes.ReservationFlow.RejectPaymentProof), 20, true, requiresReason: true));
        rules.Add(Rule(id++, ReservationFlow, StatusTypeEnum.Reservation, ReservationStatus.PaymentSubmitted, RoleCode.Owner, WorkflowActionCode.Reservation.Cancel, "Rezervasiyanı ləğv et", "POST", WorkflowEndpoint(ApiRoutes.ReservationFlow.CancelForRestaurant), 90, true));
    }

    private static void AddPaymentInstructionReservationRules(List<WorkflowActionRule> rules, ref int id)
    {
        rules.Add(Rule(id++, ReservationFlow, StatusTypeEnum.Reservation, ReservationStatus.PendingPayment, RoleCode.Manager, WorkflowActionCode.Reservation.SendPaymentInstruction, "Ödəniş məlumatı göndər", "POST", WorkflowEndpoint(ApiRoutes.ReservationFlow.SendPaymentInstruction), 10));
        rules.Add(Rule(id++, ReservationFlow, StatusTypeEnum.Reservation, ReservationStatus.PendingPayment, RoleCode.Owner, WorkflowActionCode.Reservation.SendPaymentInstruction, "Ödəniş məlumatı göndər", "POST", WorkflowEndpoint(ApiRoutes.ReservationFlow.SendPaymentInstruction), 10));
    }

    private static void AddReservationResponseRules(List<WorkflowActionRule> rules, ref int id)
    {
        rules.Add(Rule(id++, ReservationFlow, StatusTypeEnum.Reservation, ReservationStatus.AwaitingPaymentInstruction, RoleCode.Customer, WorkflowActionCode.Reservation.Cancel, "Rezervasiyanı ləğv et", "POST", WorkflowEndpoint(ApiRoutes.ReservationFlow.Cancel), 90, true));
        rules.Add(Rule(id++, ReservationFlow, StatusTypeEnum.Reservation, ReservationStatus.AwaitingPaymentInstruction, RoleCode.Manager, WorkflowActionCode.Reservation.Cancel, "Rezervasiyanı ləğv et", "POST", WorkflowEndpoint(ApiRoutes.ReservationFlow.CancelForRestaurant), 90, true));
        rules.Add(Rule(id++, ReservationFlow, StatusTypeEnum.Reservation, ReservationStatus.AwaitingPaymentInstruction, RoleCode.SuperAdmin, WorkflowActionCode.Reservation.Cancel, "Rezervasiyanı ləğv et", "POST", WorkflowEndpoint(ApiRoutes.ReservationFlow.CancelForRestaurant), 90, true));
        rules.Add(Rule(id++, ReservationFlow, StatusTypeEnum.Reservation, ReservationStatus.AwaitingPaymentInstruction, RoleCode.Owner, WorkflowActionCode.Reservation.Cancel, "Rezervasiyanı ləğv et", "POST", WorkflowEndpoint(ApiRoutes.ReservationFlow.CancelForRestaurant), 90, true));
        rules.Add(Rule(id++, ReservationFlow, StatusTypeEnum.Reservation, ReservationStatus.AwaitingPaymentInstruction, RoleCode.Manager, WorkflowActionCode.Reservation.SendPaymentInstruction, "Ödəniş məlumatı göndər", "POST", WorkflowEndpoint(ApiRoutes.ReservationFlow.SendPaymentInstruction), 10));
        rules.Add(Rule(id++, ReservationFlow, StatusTypeEnum.Reservation, ReservationStatus.AwaitingPaymentInstruction, RoleCode.Owner, WorkflowActionCode.Reservation.SendPaymentInstruction, "Ödəniş məlumatı göndər", "POST", WorkflowEndpoint(ApiRoutes.ReservationFlow.SendPaymentInstruction), 10));
    }

    private static void AddOrderRules(List<WorkflowActionRule> rules, ref int id)
    {
        rules.Add(Rule(id++, OrderFlow, StatusTypeEnum.Order, OrderStatus.Created, RoleCode.Waiter, WorkflowActionCode.Order.SendToKitchen, "Mətbəxə göndər", "POST", "/api/v1/restaurants/{restaurantId}/orders/{orderId}/send-to-kitchen", 10));
        rules.Add(Rule(id++, OrderFlow, StatusTypeEnum.Order, OrderStatus.Created, RoleCode.Manager, WorkflowActionCode.Order.Cancel, "Sifarişi ləğv et", "POST", "/api/v1/restaurants/{restaurantId}/orders/{orderId}/cancel", 90, true));
        rules.Add(Rule(id++, OrderFlow, StatusTypeEnum.Order, OrderStatus.Ready, RoleCode.Waiter, WorkflowActionCode.Order.Serve, "Servis edildi", "POST", "/api/v1/restaurants/{restaurantId}/orders/{orderId}/serve", 10));
        rules.Add(Rule(id++, OrderFlow, StatusTypeEnum.Order, OrderStatus.Served, RoleCode.Waiter, WorkflowActionCode.Order.Close, "Sifarişi bağla", "POST", "/api/v1/restaurants/{restaurantId}/orders/{orderId}/close", 80, true));
        rules.Add(Rule(id++, OrderFlow, StatusTypeEnum.Order, OrderStatus.Served, RoleCode.Manager, WorkflowActionCode.Order.Close, "Sifarişi bağla", "POST", "/api/v1/restaurants/{restaurantId}/orders/{orderId}/close", 80, true));
    }

    private static void AddKitchenRules(List<WorkflowActionRule> rules, ref int id)
    {
        rules.Add(Rule(id++, KitchenFlow, StatusTypeEnum.Order, OrderStatus.Created, RoleCode.Kitchen, WorkflowActionCode.Kitchen.Accept, "Sifarişi qəbul et", "POST", "/api/v1/restaurants/{restaurantId}/kitchen/orders/{orderId}/accept", 10));
        rules.Add(Rule(id++, KitchenFlow, StatusTypeEnum.Order, OrderStatus.Accepted, RoleCode.Kitchen, WorkflowActionCode.Kitchen.StartPreparing, "Hazırlamağa başla", "POST", "/api/v1/restaurants/{restaurantId}/kitchen/orders/{orderId}/start", 10));
        rules.Add(Rule(id++, KitchenFlow, StatusTypeEnum.Order, OrderStatus.Preparing, RoleCode.Kitchen, WorkflowActionCode.Kitchen.MarkReady, "Hazırdır", "POST", "/api/v1/restaurants/{restaurantId}/kitchen/orders/{orderId}/ready", 10));
    }

    private static void AddPaymentRules(List<WorkflowActionRule> rules, ref int id)
    {
        rules.Add(Rule(id++, PaymentFlow, StatusTypeEnum.PaymentStatus, PaymentStatus.Pending, RoleCode.Customer, WorkflowActionCode.Payment.Pay, "Ödəniş et", "POST", "/api/v1/restaurants/{restaurantId}/payments/{paymentId}/pay", 10, isEnabled: false));
        rules.Add(Rule(id++, PaymentFlow, StatusTypeEnum.PaymentStatus, PaymentStatus.Pending, RoleCode.Waiter, WorkflowActionCode.Payment.MarkPaid, "Fiziki ödənişi təsdiqlə", "POST", "/api/v1/restaurants/{restaurantId}/payments/{paymentId}/mark-paid", 20, true));
        rules.Add(Rule(id++, PaymentFlow, StatusTypeEnum.PaymentStatus, PaymentStatus.Pending, RoleCode.Manager, WorkflowActionCode.Payment.Cancel, "Ödənişi ləğv et", "POST", "/api/v1/restaurants/{restaurantId}/payments/{paymentId}/cancel", 90, true));
        rules.Add(Rule(id++, PaymentFlow, StatusTypeEnum.PaymentStatus, PaymentStatus.Failed, RoleCode.Customer, WorkflowActionCode.Payment.Retry, "Yenidən ödə", "POST", "/api/v1/restaurants/{restaurantId}/payments/{paymentId}/retry", 10));
        rules.Add(Rule(id++, PaymentFlow, StatusTypeEnum.PaymentStatus, PaymentStatus.Paid, RoleCode.Manager, WorkflowActionCode.Payment.Refund, "Geri qaytar", "POST", "/api/v1/restaurants/{restaurantId}/payments/{paymentId}/refund", 90, true));
        rules.Add(Rule(id++, PaymentFlow, StatusTypeEnum.PaymentStatus, PaymentStatus.Paid, RoleCode.SuperAdmin, WorkflowActionCode.Payment.Refund, "Geri qaytar", "POST", "/api/v1/admin/restaurants/{restaurantId}/payments/{paymentId}/refund", 90, true));
    }

    private static WorkflowActionRule Rule<TStatus>(
        int id,
        string flowCode,
        StatusTypeEnum statusType,
        TStatus status,
        RoleCode role,
        string actionCode,
        string label,
        string httpMethod,
        string endpointTemplate,
        int sortOrder,
        bool requiresConfirmation = false,
        bool requiresReason = false,
        bool isEnabled = true)
        where TStatus : struct, Enum
        => new()
        {
            Id = id,
            FlowCode = flowCode,
            StatusId = ((int)statusType * 1000) + Convert.ToInt32(status),
            RoleId = (int)role,
            ActionCode = actionCode,
            Label = label,
            HttpMethod = httpMethod,
            EndpointTemplate = endpointTemplate,
            SortOrder = sortOrder,
            RequiresConfirmation = requiresConfirmation,
            RequiresReason = requiresReason,
            IsEnabled = isEnabled
        };
}
