using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using StatusTypeEnum = ECafe.Domain.Enums.StatusType;

namespace ECafe.Infrastructure.Seeders;

public static class WorkflowActionRuleSeeder
{
    private const string ContractFlow = "contract";
    private const string ReservationFlow = "reservation";
    private const string OrderFlow = "order";
    private const string KitchenFlow = "kitchen";
    private const string PaymentFlow = "payment";

    public static void Seed(ModelBuilder modelBuilder)
    {
        var rules = new List<WorkflowActionRule>();
        var id = 1;

        AddContractRules(rules, ref id);
        AddReservationRules(rules, ref id);
        AddOrderRules(rules, ref id);
        AddKitchenRules(rules, ref id);
        AddPaymentRules(rules, ref id);

        modelBuilder.Entity<WorkflowActionRule>().HasData(rules);
    }

    private static void AddContractRules(List<WorkflowActionRule> rules, ref int id)
    {
        rules.Add(Rule(id++, ContractFlow, StatusTypeEnum.Contract, ContractStatus.Draft, RoleCode.SuperAdmin, "sendForSignature", "Sahibkar təsdiqinə göndər", "POST", "/api/v1/admin/restaurants/{restaurantId}/contracts/{contractId}/send-for-signature", 10));
        rules.Add(Rule(id++, ContractFlow, StatusTypeEnum.Contract, ContractStatus.Draft, RoleCode.SuperAdmin, "terminate", "Müqaviləni ləğv et", "POST", "/api/v1/admin/restaurants/{restaurantId}/contracts/{contractId}/terminate", 90, true));
        rules.Add(Rule(id++, ContractFlow, StatusTypeEnum.Contract, ContractStatus.PendingSignature, RoleCode.Owner, "approve", "Müqaviləni təsdiqlə", "POST", "/api/v1/restaurants/{restaurantId}/contracts/{contractId}/approve", 10, true));
        rules.Add(Rule(id++, ContractFlow, StatusTypeEnum.Contract, ContractStatus.PendingSignature, RoleCode.SuperAdmin, "terminate", "Müqaviləni ləğv et", "POST", "/api/v1/admin/restaurants/{restaurantId}/contracts/{contractId}/terminate", 90, true));
        rules.Add(Rule(id++, ContractFlow, StatusTypeEnum.Contract, ContractStatus.OwnerApproved, RoleCode.SuperAdmin, "activate", "Müqaviləni aktivləşdir", "POST", "/api/v1/admin/restaurants/{restaurantId}/contracts/{contractId}/activate", 10));
        rules.Add(Rule(id++, ContractFlow, StatusTypeEnum.Contract, ContractStatus.OwnerApproved, RoleCode.SuperAdmin, "terminate", "Müqaviləni ləğv et", "POST", "/api/v1/admin/restaurants/{restaurantId}/contracts/{contractId}/terminate", 90, true));
        rules.Add(Rule(id++, ContractFlow, StatusTypeEnum.Contract, ContractStatus.Active, RoleCode.SuperAdmin, "terminate", "Müqaviləni ləğv et", "POST", "/api/v1/admin/restaurants/{restaurantId}/contracts/{contractId}/terminate", 90, true));
    }

    private static void AddReservationRules(List<WorkflowActionRule> rules, ref int id)
    {
        rules.Add(Rule(id++, ReservationFlow, StatusTypeEnum.Reservation, ReservationStatus.PendingDeposit, RoleCode.Customer, "cancel", "Rezervasiyanı ləğv et", "POST", "/api/v1/restaurants/{restaurantId}/reservations/{reservationId}/cancel", 90, true));
        rules.Add(Rule(id++, ReservationFlow, StatusTypeEnum.Reservation, ReservationStatus.PendingDeposit, RoleCode.Manager, "cancel", "Rezervasiyanı ləğv et", "POST", "/api/v1/restaurants/{restaurantId}/reservations/{reservationId}/cancel", 90, true));
        rules.Add(Rule(id++, ReservationFlow, StatusTypeEnum.Reservation, ReservationStatus.PendingDeposit, RoleCode.SuperAdmin, "cancel", "Rezervasiyanı ləğv et", "POST", "/api/v1/admin/restaurants/{restaurantId}/reservations/{reservationId}/cancel", 90, true));
        rules.Add(Rule(id++, ReservationFlow, StatusTypeEnum.Reservation, ReservationStatus.Reserved, RoleCode.Waiter, "checkIn", "Müştərini oturt", "POST", "/api/v1/restaurants/{restaurantId}/reservations/{reservationId}/check-in", 10));
        rules.Add(Rule(id++, ReservationFlow, StatusTypeEnum.Reservation, ReservationStatus.Reserved, RoleCode.Manager, "checkIn", "Müştərini oturt", "POST", "/api/v1/restaurants/{restaurantId}/reservations/{reservationId}/check-in", 10));
        rules.Add(Rule(id++, ReservationFlow, StatusTypeEnum.Reservation, ReservationStatus.Reserved, RoleCode.Manager, "markNoShow", "Gəlmədi kimi qeyd et", "POST", "/api/v1/restaurants/{restaurantId}/reservations/{reservationId}/no-show", 70, true));
        rules.Add(Rule(id++, ReservationFlow, StatusTypeEnum.Reservation, ReservationStatus.Reserved, RoleCode.Customer, "cancel", "Rezervasiyanı ləğv et", "POST", "/api/v1/restaurants/{restaurantId}/reservations/{reservationId}/cancel", 90, true));
        rules.Add(Rule(id++, ReservationFlow, StatusTypeEnum.Reservation, ReservationStatus.Seated, RoleCode.Waiter, "complete", "Rezervasiyanı tamamla", "POST", "/api/v1/restaurants/{restaurantId}/reservations/{reservationId}/complete", 80, true));
        rules.Add(Rule(id++, ReservationFlow, StatusTypeEnum.Reservation, ReservationStatus.Seated, RoleCode.Manager, "complete", "Rezervasiyanı tamamla", "POST", "/api/v1/restaurants/{restaurantId}/reservations/{reservationId}/complete", 80, true));
    }

    private static void AddOrderRules(List<WorkflowActionRule> rules, ref int id)
    {
        rules.Add(Rule(id++, OrderFlow, StatusTypeEnum.Order, OrderStatus.Created, RoleCode.Waiter, "sendToKitchen", "Mətbəxə göndər", "POST", "/api/v1/restaurants/{restaurantId}/orders/{orderId}/send-to-kitchen", 10));
        rules.Add(Rule(id++, OrderFlow, StatusTypeEnum.Order, OrderStatus.Created, RoleCode.Manager, "cancel", "Sifarişi ləğv et", "POST", "/api/v1/restaurants/{restaurantId}/orders/{orderId}/cancel", 90, true));
        rules.Add(Rule(id++, OrderFlow, StatusTypeEnum.Order, OrderStatus.Ready, RoleCode.Waiter, "serve", "Servis edildi", "POST", "/api/v1/restaurants/{restaurantId}/orders/{orderId}/serve", 10));
        rules.Add(Rule(id++, OrderFlow, StatusTypeEnum.Order, OrderStatus.Served, RoleCode.Waiter, "close", "Sifarişi bağla", "POST", "/api/v1/restaurants/{restaurantId}/orders/{orderId}/close", 80, true));
        rules.Add(Rule(id++, OrderFlow, StatusTypeEnum.Order, OrderStatus.Served, RoleCode.Manager, "close", "Sifarişi bağla", "POST", "/api/v1/restaurants/{restaurantId}/orders/{orderId}/close", 80, true));
    }

    private static void AddKitchenRules(List<WorkflowActionRule> rules, ref int id)
    {
        rules.Add(Rule(id++, KitchenFlow, StatusTypeEnum.Order, OrderStatus.Created, RoleCode.Kitchen, "accept", "Sifarişi qəbul et", "POST", "/api/v1/restaurants/{restaurantId}/kitchen/orders/{orderId}/accept", 10));
        rules.Add(Rule(id++, KitchenFlow, StatusTypeEnum.Order, OrderStatus.Accepted, RoleCode.Kitchen, "startPreparing", "Hazırlamağa başla", "POST", "/api/v1/restaurants/{restaurantId}/kitchen/orders/{orderId}/start", 10));
        rules.Add(Rule(id++, KitchenFlow, StatusTypeEnum.Order, OrderStatus.Preparing, RoleCode.Kitchen, "markReady", "Hazırdır", "POST", "/api/v1/restaurants/{restaurantId}/kitchen/orders/{orderId}/ready", 10));
    }

    private static void AddPaymentRules(List<WorkflowActionRule> rules, ref int id)
    {
        rules.Add(Rule(id++, PaymentFlow, StatusTypeEnum.PaymentStatus, PaymentStatus.Pending, RoleCode.Customer, "pay", "Ödəniş et", "POST", "/api/v1/restaurants/{restaurantId}/payments/{paymentId}/pay", 10));
        rules.Add(Rule(id++, PaymentFlow, StatusTypeEnum.PaymentStatus, PaymentStatus.Pending, RoleCode.Waiter, "markPaid", "Fiziki ödənişi təsdiqlə", "POST", "/api/v1/restaurants/{restaurantId}/payments/{paymentId}/mark-paid", 20, true));
        rules.Add(Rule(id++, PaymentFlow, StatusTypeEnum.PaymentStatus, PaymentStatus.Pending, RoleCode.Manager, "cancel", "Ödənişi ləğv et", "POST", "/api/v1/restaurants/{restaurantId}/payments/{paymentId}/cancel", 90, true));
        rules.Add(Rule(id++, PaymentFlow, StatusTypeEnum.PaymentStatus, PaymentStatus.Failed, RoleCode.Customer, "retry", "Yenidən ödə", "POST", "/api/v1/restaurants/{restaurantId}/payments/{paymentId}/retry", 10));
        rules.Add(Rule(id++, PaymentFlow, StatusTypeEnum.PaymentStatus, PaymentStatus.Paid, RoleCode.Manager, "refund", "Geri qaytar", "POST", "/api/v1/restaurants/{restaurantId}/payments/{paymentId}/refund", 90, true));
        rules.Add(Rule(id++, PaymentFlow, StatusTypeEnum.PaymentStatus, PaymentStatus.Paid, RoleCode.SuperAdmin, "refund", "Geri qaytar", "POST", "/api/v1/admin/restaurants/{restaurantId}/payments/{paymentId}/refund", 90, true));
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
        bool requiresConfirmation = false)
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
            IsEnabled = true
        };
}
