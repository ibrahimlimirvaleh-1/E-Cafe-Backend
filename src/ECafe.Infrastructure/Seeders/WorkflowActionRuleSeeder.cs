using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Domain.Workflow;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
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

    /// <summary>Müqavilə mərhələləri üçün mövcud rol, əməliyyat və endpoint toxum qeydlərini eyni ID ardıcıllığında qurur.</summary>
    private static void AddContractRules(List<WorkflowActionRule> rules, ref int id)
    {
        var flowCode = WorkflowFlowCode.FromStatusType(StatusTypeEnum.Contract);
        rules.Add(Rule(id++, flowCode, StatusTypeEnum.Contract, ContractStatus.Draft, RoleCode.SuperAdmin, ContractActionCode.SendForSignature, "Sahibkar təsdiqinə göndər", HttpMethods.Post, "/api/v1/admin/restaurants/{restaurantId}/contracts/{contractId}/send-for-signature", 10));
        rules.Add(Rule(id++, flowCode, StatusTypeEnum.Contract, ContractStatus.Draft, RoleCode.SuperAdmin, ContractActionCode.Terminate, "Müqaviləni ləğv et", HttpMethods.Post, "/api/v1/admin/restaurants/{restaurantId}/contracts/{contractId}/terminate", 90, true));
        rules.Add(Rule(id++, flowCode, StatusTypeEnum.Contract, ContractStatus.PendingSignature, RoleCode.Owner, ContractActionCode.Approve, "Müqaviləni təsdiqlə", HttpMethods.Post, "/api/v1/restaurants/{restaurantId}/contracts/{contractId}/approve", 10, true));
        rules.Add(Rule(id++, flowCode, StatusTypeEnum.Contract, ContractStatus.PendingSignature, RoleCode.SuperAdmin, ContractActionCode.Terminate, "Müqaviləni ləğv et", HttpMethods.Post, "/api/v1/admin/restaurants/{restaurantId}/contracts/{contractId}/terminate", 90, true));
        rules.Add(Rule(id++, flowCode, StatusTypeEnum.Contract, ContractStatus.OwnerApproved, RoleCode.SuperAdmin, ContractActionCode.Activate, "Müqaviləni aktivləşdir", HttpMethods.Post, "/api/v1/admin/restaurants/{restaurantId}/contracts/{contractId}/activate", 10));
        rules.Add(Rule(id++, flowCode, StatusTypeEnum.Contract, ContractStatus.OwnerApproved, RoleCode.SuperAdmin, ContractActionCode.Terminate, "Müqaviləni ləğv et", HttpMethods.Post, "/api/v1/admin/restaurants/{restaurantId}/contracts/{contractId}/terminate", 90, true));
        rules.Add(Rule(id++, flowCode, StatusTypeEnum.Contract, ContractStatus.Active, RoleCode.SuperAdmin, ContractActionCode.Terminate, "Müqaviləni ləğv et", HttpMethods.Post, "/api/v1/admin/restaurants/{restaurantId}/contracts/{contractId}/terminate", 90, true));
    }

    /// <summary>Planlanmış müqavilənin əvvəlki ləğv qaydasını mərkəzi əməliyyat kodu ilə saxlayır.</summary>
    private static void AddScheduledContractRules(List<WorkflowActionRule> rules, ref int id)
    {
        var flowCode = WorkflowFlowCode.FromStatusType(StatusTypeEnum.Contract);
        rules.Add(Rule(id++, flowCode, StatusTypeEnum.Contract, ContractStatus.Scheduled, RoleCode.SuperAdmin, ContractActionCode.Terminate, "Müqaviləni ləğv et", HttpMethods.Post, "/api/v1/admin/restaurants/{restaurantId}/contracts/{contractId}/terminate", 90, true));
    }

    /// <summary>İlkin rezervasiya ləğv qaydalarının rol və təsdiq şərtlərini dəyişmədən qurur.</summary>
    private static void AddReservationRules(List<WorkflowActionRule> rules, ref int id)
    {
        var flowCode = WorkflowFlowCode.FromStatusType(StatusTypeEnum.Reservation);
        rules.Add(Rule(id++, flowCode, StatusTypeEnum.Reservation, ReservationStatus.PendingPayment, RoleCode.Customer, ReservationActionCode.Cancel, "Rezervasiyanı ləğv et", HttpMethods.Post, "/api/v1/public/reservations/{reservationId}/cancel", 90, true));
        rules.Add(Rule(id++, flowCode, StatusTypeEnum.Reservation, ReservationStatus.PendingPayment, RoleCode.Manager, ReservationActionCode.Cancel, "Rezervasiyanı ləğv et", HttpMethods.Post, "/api/v1/restaurants/{restaurantId}/reservations/{reservationId}/cancel", 90, true));
        rules.Add(Rule(id++, flowCode, StatusTypeEnum.Reservation, ReservationStatus.PendingPayment, RoleCode.SuperAdmin, ReservationActionCode.Cancel, "Rezervasiyanı ləğv et", HttpMethods.Post, "/api/v1/restaurants/{restaurantId}/reservations/{reservationId}/cancel", 90, true));
        rules.Add(Rule(id++, ReservationFlow, StatusTypeEnum.Reservation, ReservationStatus.Confirmed, RoleCode.Customer, ReservationActionCode.Cancel, "Rezervasiyanı ləğv et", HttpMethods.Post, "/api/v1/public/reservations/{reservationId}/cancel", 90, true));
    }

    /// <summary>Gəliş, əyləşmə və sessiya bağlanması qaydalarını əvvəlki status/rol sərhədləri ilə qurur.</summary>
    private static void AddReservationSessionRules(List<WorkflowActionRule> rules, ref int id)
    {
        rules.Add(Rule(id++, ReservationFlow, StatusTypeEnum.Reservation, ReservationStatus.Confirmed, RoleCode.Manager, ReservationActionCode.MarkArrived, MarkArrivalLabel, HttpMethods.Post, WorkflowEndpoint(ApiRoutes.ReservationFlow.MarkArrived), 10));
        rules.Add(Rule(id++, ReservationFlow, StatusTypeEnum.Reservation, ReservationStatus.Confirmed, RoleCode.Waiter, ReservationActionCode.CheckIn, SeatGuestLabel, HttpMethods.Post, WorkflowEndpoint(ApiRoutes.ReservationFlow.CheckIn), 20));
        rules.Add(Rule(id++, ReservationFlow, StatusTypeEnum.Reservation, ReservationStatus.Seated, RoleCode.Manager, ReservationActionCode.Complete, "Masa sessiyasını bağla", HttpMethods.Post, "/api/v1/restaurants/{restaurantId}/reservations/{reservationId}/complete", 10, true));
        rules.Add(Rule(id++, ReservationFlow, StatusTypeEnum.Reservation, ReservationStatus.Seated, RoleCode.Owner, ReservationActionCode.Complete, "Masa sessiyasını bağla", HttpMethods.Post, "/api/v1/restaurants/{restaurantId}/reservations/{reservationId}/complete", 10, true));
    }

    /// <summary>Təsdiqlənmiş rezervasiya ləğvi və müştərinin refund sorğusu üçün əvvəlki qaydaları saxlayır.</summary>
    private static void AddReservationRefundRules(List<WorkflowActionRule> rules, ref int id)
    {
        rules.Add(Rule(id++, ReservationFlow, StatusTypeEnum.Reservation, ReservationStatus.Confirmed, RoleCode.Manager, ReservationActionCode.Cancel, "Rezervasiyanı ləğv et", HttpMethods.Post, "/api/v1/restaurants/{restaurantId}/reservations/{reservationId}/cancel", 90, true));
        rules.Add(Rule(id++, ReservationFlow, StatusTypeEnum.Reservation, ReservationStatus.Confirmed, RoleCode.Owner, ReservationActionCode.Cancel, "Rezervasiyanı ləğv et", HttpMethods.Post, "/api/v1/restaurants/{restaurantId}/reservations/{reservationId}/cancel", 90, true));
        rules.Add(Rule(id++, ReservationFlow, StatusTypeEnum.Reservation, ReservationStatus.Cancelled, RoleCode.Customer, ReservationActionCode.RequestRefund, "Geri ödəniş soruş", HttpMethods.Post, "/api/v1/public/reservations/{reservationId}/refunds", 100, true));
    }

    /// <summary>Refund rekvizitinin təqdim edilməsi üçün əvvəlki müştəri əməliyyatını qurur.</summary>
    private static void AddRefundPayoutDetailsRules(List<WorkflowActionRule> rules, ref int id)
    {
        rules.Add(Rule(
            id++,
            RefundFlow,
            StatusTypeEnum.Refund,
            RefundStatus.AwaitingPayoutDetails,
            RoleCode.Customer,
            RefundActionCode.SubmitPayoutDetails,
            "Geri ödəniş məlumatını göndər",
            HttpMethods.Post,
            "/api/v1/public/refunds/{refundId}/payout-details",
            10));
    }

    /// <summary>Verilmiş refund statusu üçün əvvəlki transfer qaydalarını və təsdiq tələbini saxlayır.</summary>
    private static void AddRefundTransferRules(List<WorkflowActionRule> rules, ref int id)
    {
        var endpoint = "/api/v1/restaurants/{restaurantId}/refunds/{refundId}/transfers";

        AddRefundTransferRules(rules, ref id, RefundStatus.ReadyForPayout, endpoint);
        AddRefundTransferRules(rules, ref id, RefundStatus.Disputed, endpoint);
    }

    /// <summary>Verilmiş refund statusu üçün əvvəlki transfer qaydalarını və təsdiq tələbini saxlayır.</summary>
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
            RefundActionCode.SubmitTransfer,
            "Geri ödəniş çekini göndər",
            HttpMethods.Post,
            endpoint,
            10,
            requiresConfirmation: true));
        rules.Add(Rule(
            id++,
            RefundFlow,
            StatusTypeEnum.Refund,
            status,
            RoleCode.Owner,
            RefundActionCode.SubmitTransfer,
            "Geri ödəniş çekini göndər",
            HttpMethods.Post,
            endpoint,
            10,
            requiresConfirmation: true));
    }

    /// <summary>Müştərinin refund təsdiq və etiraz qaydalarını əvvəlki səbəb tələbi ilə qurur.</summary>
    private static void AddRefundCustomerReviewRules(List<WorkflowActionRule> rules, ref int id)
    {
        rules.Add(Rule(
            id++, RefundFlow, StatusTypeEnum.Refund, RefundStatus.Processing,
            RoleCode.Customer, RefundActionCode.ConfirmTransfer,
            "Geri ödənişi təsdiqlə", HttpMethods.Post,
            "/api/v1/public/refunds/{refundId}/transfers/confirm", 10,
            requiresConfirmation: true));

        rules.Add(Rule(
            id++, RefundFlow, StatusTypeEnum.Refund, RefundStatus.Processing,
            RoleCode.Customer, RefundActionCode.DisputeTransfer,
            "Geri ödənişə etiraz et", HttpMethods.Post,
            "/api/v1/public/refunds/{refundId}/transfers/dispute", 20,
            requiresConfirmation: true, requiresReason: true));
    }

    /// <summary>Menecer və sahibkarın uyğun refund statuslarında rekvizitə baxış qaydalarını saxlayır.</summary>
    private static void AddRefundPayoutViewRules(List<WorkflowActionRule> rules, ref int id)
    {
        const string endpoint = "/api/v1/restaurants/{restaurantId}/refunds/{refundId}/payout-details";

        foreach (var status in new[] { RefundStatus.ReadyForPayout, RefundStatus.Processing, RefundStatus.Disputed })
        {
            foreach (var role in new[] { RoleCode.Manager, RoleCode.Owner })
            {
                rules.Add(Rule(
                    id++, RefundFlow, StatusTypeEnum.Refund, status, role,
                    RefundActionCode.ViewPayoutDetails,
                    "Ödəniş rekvizitinə bax", HttpMethods.Get, endpoint, 5));
            }
        }
    }

    /// <summary>Köhnə çek axınının mövcud toxum qeydlərini eyni status və kodlarla saxlayır; yeni axın aktivləşdirmir.</summary>
    private static void AddReceiptReservationRules(List<WorkflowActionRule> rules, ref int id)
    {
        rules.Add(Rule(id++, ReservationFlow, StatusTypeEnum.Reservation, ReservationStatus.PendingPayment, RoleCode.Customer, ReservationActionCode.SubmitPaymentProof, "Ödəniş çekini göndər", HttpMethods.Post, "/api/v1/restaurants/{restaurantId}/reservations/{reservationId}/payment-proofs", 10));
        rules.Add(Rule(id++, ReservationFlow, StatusTypeEnum.Reservation, ReservationStatus.PaymentSubmitted, RoleCode.Manager, ReservationActionCode.ApprovePaymentProof, "Ödənişi təsdiqlə", HttpMethods.Post, "/api/v1/restaurants/{restaurantId}/reservations/{reservationId}/payment-proofs/approve", 10));
        rules.Add(Rule(id++, ReservationFlow, StatusTypeEnum.Reservation, ReservationStatus.PaymentSubmitted, RoleCode.Manager, ReservationActionCode.RejectPaymentProof, "Ödənişi rədd et", HttpMethods.Post, "/api/v1/restaurants/{restaurantId}/reservations/{reservationId}/payment-proofs/reject", 20, true, requiresReason: true));
        rules.Add(Rule(id++, ReservationFlow, StatusTypeEnum.Reservation, ReservationStatus.PaymentSubmitted, RoleCode.Manager, ReservationActionCode.Cancel, "Rezervasiyanı ləğv et", HttpMethods.Post, "/api/v1/restaurants/{restaurantId}/reservations/{reservationId}/cancel", 90, true));
    }

    /// <summary>Depozitdən imtina əməliyyatının mövcud rol, təsdiq və səbəb tələblərini saxlayır.</summary>
    private static void AddReservationDepositWaiverRules(List<WorkflowActionRule> rules, ref int id)
    {
        const string endpoint = "/api/v1/restaurants/{restaurantId}/reservations/{reservationId}/deposit-waiver";

        foreach (var role in new[] { RoleCode.Manager, RoleCode.Owner })
        {
            rules.Add(Rule(
                id++, ReservationFlow, StatusTypeEnum.Reservation,
                ReservationStatus.AwaitingPaymentInstruction, role,
                ReservationActionCode.WaiveDeposit,
                "Depozitdən imtina et", HttpMethods.Post, endpoint, 20,
                requiresConfirmation: true, requiresReason: true));
        }
    }

    /// <summary>Sahibkar və ofisiantın gəliş qeyd etmə qaydalarını dəyişmədən qurur.</summary>
    private static void AddReservationArrivalAndSeatingRules(List<WorkflowActionRule> rules, ref int id)
    {
        var endpoint = WorkflowEndpoint(ApiRoutes.ReservationFlow.MarkArrived);
        foreach (var role in new[] { RoleCode.Owner, RoleCode.Waiter })
        {
            rules.Add(Rule(id++, ReservationFlow, StatusTypeEnum.Reservation,
                ReservationStatus.Confirmed, role, ReservationActionCode.MarkArrived,
                MarkArrivalLabel, HttpMethods.Post, endpoint, 10));
        }
    }

    private static string WorkflowEndpoint(string apiRoute)
        => "/" + apiRoute.Replace(":int", string.Empty, StringComparison.Ordinal);

    /// <summary>Sahibkarın rezervasiya və çek qərarlarının əvvəlki status qaydalarını saxlayır.</summary>
    private static void AddOwnerReservationRules(List<WorkflowActionRule> rules, ref int id)
    {
        rules.Add(Rule(id++, ReservationFlow, StatusTypeEnum.Reservation, ReservationStatus.PendingPayment, RoleCode.Owner, ReservationActionCode.Cancel, "Rezervasiyanı ləğv et", HttpMethods.Post, "/api/v1/restaurants/{restaurantId}/reservations/{reservationId}/cancel", 90, true));
        rules.Add(Rule(id++, ReservationFlow, StatusTypeEnum.Reservation, ReservationStatus.PaymentSubmitted, RoleCode.Owner, ReservationActionCode.ApprovePaymentProof, "Ödənişi təsdiqlə", HttpMethods.Post, "/api/v1/restaurants/{restaurantId}/reservations/{reservationId}/payment-proofs/approve", 10));
        rules.Add(Rule(id++, ReservationFlow, StatusTypeEnum.Reservation, ReservationStatus.PaymentSubmitted, RoleCode.Owner, ReservationActionCode.RejectPaymentProof, "Ödənişi rədd et", HttpMethods.Post, "/api/v1/restaurants/{restaurantId}/reservations/{reservationId}/payment-proofs/reject", 20, true, requiresReason: true));
        rules.Add(Rule(id++, ReservationFlow, StatusTypeEnum.Reservation, ReservationStatus.PaymentSubmitted, RoleCode.Owner, ReservationActionCode.Cancel, "Rezervasiyanı ləğv et", HttpMethods.Post, "/api/v1/restaurants/{restaurantId}/reservations/{reservationId}/cancel", 90, true));
    }

    /// <summary>Menecer və sahibkarın depozit məlumatı göndərmə qaydalarını qurur.</summary>
    private static void AddPaymentInstructionReservationRules(List<WorkflowActionRule> rules, ref int id)
    {
        rules.Add(Rule(id++, ReservationFlow, StatusTypeEnum.Reservation, ReservationStatus.PendingPayment, RoleCode.Manager, ReservationActionCode.SendPaymentInstruction, "Ödəniş məlumatı göndər", HttpMethods.Post, "/api/v1/restaurants/{restaurantId}/reservations/{reservationId}/payment-instructions", 10));
        rules.Add(Rule(id++, ReservationFlow, StatusTypeEnum.Reservation, ReservationStatus.PendingPayment, RoleCode.Owner, ReservationActionCode.SendPaymentInstruction, "Ödəniş məlumatı göndər", HttpMethods.Post, "/api/v1/restaurants/{restaurantId}/reservations/{reservationId}/payment-instructions", 10));
    }

    /// <summary>Cavab gözləyən rezervasiyada əvvəlki ləğv və məlumat göndərmə əməliyyatlarını saxlayır.</summary>
    private static void AddReservationResponseRules(List<WorkflowActionRule> rules, ref int id)
    {
        rules.Add(Rule(id++, ReservationFlow, StatusTypeEnum.Reservation, ReservationStatus.AwaitingPaymentInstruction, RoleCode.Customer, ReservationActionCode.Cancel, "Rezervasiyanı ləğv et", HttpMethods.Post, "/api/v1/public/reservations/{reservationId}/cancel", 90, true));
        rules.Add(Rule(id++, ReservationFlow, StatusTypeEnum.Reservation, ReservationStatus.AwaitingPaymentInstruction, RoleCode.Manager, ReservationActionCode.Cancel, "Rezervasiyanı ləğv et", HttpMethods.Post, "/api/v1/restaurants/{restaurantId}/reservations/{reservationId}/cancel", 90, true));
        rules.Add(Rule(id++, ReservationFlow, StatusTypeEnum.Reservation, ReservationStatus.AwaitingPaymentInstruction, RoleCode.SuperAdmin, ReservationActionCode.Cancel, "Rezervasiyanı ləğv et", HttpMethods.Post, "/api/v1/restaurants/{restaurantId}/reservations/{reservationId}/cancel", 90, true));
        rules.Add(Rule(id++, ReservationFlow, StatusTypeEnum.Reservation, ReservationStatus.AwaitingPaymentInstruction, RoleCode.Owner, ReservationActionCode.Cancel, "Rezervasiyanı ləğv et", HttpMethods.Post, "/api/v1/restaurants/{restaurantId}/reservations/{reservationId}/cancel", 90, true));
        rules.Add(Rule(id++, ReservationFlow, StatusTypeEnum.Reservation, ReservationStatus.AwaitingPaymentInstruction, RoleCode.Manager, ReservationActionCode.SendPaymentInstruction, "Ödəniş məlumatı göndər", HttpMethods.Post, "/api/v1/restaurants/{restaurantId}/reservations/{reservationId}/payment-instructions", 10));
        rules.Add(Rule(id++, ReservationFlow, StatusTypeEnum.Reservation, ReservationStatus.AwaitingPaymentInstruction, RoleCode.Owner, ReservationActionCode.SendPaymentInstruction, "Ödəniş məlumatı göndər", HttpMethods.Post, "/api/v1/restaurants/{restaurantId}/reservations/{reservationId}/payment-instructions", 10));
    }

    /// <summary>Mövcud köhnə sifariş toxum qaydalarını qurur; bu refaktor həmin qaydaları əlavə aktivləşdirmir.</summary>
    private static void AddOrderRules(List<WorkflowActionRule> rules, ref int id)
    {
        rules.Add(Rule(id++, OrderFlow, StatusTypeEnum.Order, OrderStatus.Created, RoleCode.Waiter, "sendToKitchen", "Mətbəxə göndər", HttpMethods.Post, "/api/v1/restaurants/{restaurantId}/orders/{orderId}/send-to-kitchen", 10));
        rules.Add(Rule(id++, OrderFlow, StatusTypeEnum.Order, OrderStatus.Created, RoleCode.Manager, "cancel", "Sifarişi ləğv et", HttpMethods.Post, "/api/v1/restaurants/{restaurantId}/orders/{orderId}/cancel", 90, true));
        rules.Add(Rule(id++, OrderFlow, StatusTypeEnum.Order, OrderStatus.Ready, RoleCode.Waiter, "serve", "Servis edildi", HttpMethods.Post, "/api/v1/restaurants/{restaurantId}/orders/{orderId}/serve", 10));
        rules.Add(Rule(id++, OrderFlow, StatusTypeEnum.Order, OrderStatus.Served, RoleCode.Waiter, "close", "Sifarişi bağla", HttpMethods.Post, "/api/v1/restaurants/{restaurantId}/orders/{orderId}/close", 80, true));
        rules.Add(Rule(id++, OrderFlow, StatusTypeEnum.Order, OrderStatus.Served, RoleCode.Manager, "close", "Sifarişi bağla", HttpMethods.Post, "/api/v1/restaurants/{restaurantId}/orders/{orderId}/close", 80, true));
    }

    /// <summary>Köhnə mətbəx toxum qaydalarının status və endpoint məlumatlarını dəyişmir.</summary>
    private static void AddKitchenRules(List<WorkflowActionRule> rules, ref int id)
    {
        rules.Add(Rule(id++, KitchenFlow, StatusTypeEnum.Order, OrderStatus.Created, RoleCode.Kitchen, "accept", "Sifarişi qəbul et", HttpMethods.Post, "/api/v1/restaurants/{restaurantId}/kitchen/orders/{orderId}/accept", 10));
        rules.Add(Rule(id++, KitchenFlow, StatusTypeEnum.Order, OrderStatus.Accepted, RoleCode.Kitchen, "startPreparing", "Hazırlamağa başla", HttpMethods.Post, "/api/v1/restaurants/{restaurantId}/kitchen/orders/{orderId}/start", 10));
        rules.Add(Rule(id++, KitchenFlow, StatusTypeEnum.Order, OrderStatus.Preparing, RoleCode.Kitchen, "markReady", "Hazırdır", HttpMethods.Post, "/api/v1/restaurants/{restaurantId}/kitchen/orders/{orderId}/ready", 10));
    }

    /// <summary>Köhnə ödəniş qaydalarının isEnabled və rol sərhədlərini eyni saxlayır.</summary>
    private static void AddPaymentRules(List<WorkflowActionRule> rules, ref int id)
    {
        rules.Add(Rule(id++, PaymentFlow, StatusTypeEnum.PaymentStatus, PaymentStatus.Pending, RoleCode.Customer, "pay", "Ödəniş et", HttpMethods.Post, "/api/v1/restaurants/{restaurantId}/payments/{paymentId}/pay", 10, isEnabled: false));
        rules.Add(Rule(id++, PaymentFlow, StatusTypeEnum.PaymentStatus, PaymentStatus.Pending, RoleCode.Waiter, "markPaid", "Fiziki ödənişi təsdiqlə", HttpMethods.Post, "/api/v1/restaurants/{restaurantId}/payments/{paymentId}/mark-paid", 20, true));
        rules.Add(Rule(id++, PaymentFlow, StatusTypeEnum.PaymentStatus, PaymentStatus.Pending, RoleCode.Manager, "cancel", "Ödənişi ləğv et", HttpMethods.Post, "/api/v1/restaurants/{restaurantId}/payments/{paymentId}/cancel", 90, true));
        rules.Add(Rule(id++, PaymentFlow, StatusTypeEnum.PaymentStatus, PaymentStatus.Failed, RoleCode.Customer, "retry", "Yenidən ödə", HttpMethods.Post, "/api/v1/restaurants/{restaurantId}/payments/{paymentId}/retry", 10));
        rules.Add(Rule(id++, PaymentFlow, StatusTypeEnum.PaymentStatus, PaymentStatus.Paid, RoleCode.Manager, "refund", "Geri qaytar", HttpMethods.Post, "/api/v1/restaurants/{restaurantId}/payments/{paymentId}/refund", 90, true));
        rules.Add(Rule(id++, PaymentFlow, StatusTypeEnum.PaymentStatus, PaymentStatus.Paid, RoleCode.SuperAdmin, "refund", "Geri qaytar", HttpMethods.Post, "/api/v1/admin/restaurants/{restaurantId}/payments/{paymentId}/refund", 90, true));
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
