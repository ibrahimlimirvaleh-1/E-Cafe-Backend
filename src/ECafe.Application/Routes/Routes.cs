namespace ECafe.Application.Routes;

public static class Routes
{
    private const string Root = "api";
    private const string Version = "v1";
    private const string Base = Root + "/" + Version;

    public static class AuditLog
    {
        public const string GetRestaurantTimeline = Base + "/restaurants/{restaurantId}/audit-logs";
    }

    public static class Auth
    {
        public const string Login = Base + "/user/login";
        public const string Register = Base + "/user/register";
        public const string SetPassword = Base + "/user/set-password";
        public const string ForgotPassword = Base + "/user/forgot-password";
        public const string ResetPassword = Base + "/user/reset-password";
        public const string Refresh = Base + "/user/refresh";
        public const string Logout = Base + "/user/logout";
        public const string LogoutAll = Base + "/user/logout-all";
    }

    public static class Category
    {
        public const string GetAll = Base + "/category/{restaurantId}";
        public const string Create = Base + "/restaurants/{restaurantId}/categories";
        public const string Update = Base + "/restaurants/{restaurantId}/categories/{categoryId}";
        public const string Activate = Base + "/restaurants/{restaurantId}/categories/{categoryId}/activate";
        public const string Deactivate = Base + "/restaurants/{restaurantId}/categories/{categoryId}/deactivate";
        public const string Delete = Base + "/restaurants/{restaurantId}/categories/{categoryId}";
    }

    public static class DeveloperNotificationTest
    {
        public const string SendEmail = Base + "/developer/test/email";
        public const string SendSms = Base + "/developer/test/sms";
        public const string GetSmsBalance = Base + "/developer/test/sms/balance";
        public const string GetSmsStatus = Base + "/developer/test/sms/status/{messageId}";
    }

    public static class File
    {
        public const string Upload = Base + "/file/upload";
        public const string Delete = Base + "/file/{fileId:int}";
        public const string GetById = Base + "/file/{fileId:int}";
        public const string View = Base + "/files/{fileId:int}/view";
        public const string Download = Base + "/files/{fileId:int}/download";
        public const string GetFile = Base + "/file/getFile";
    }

    public static class Inventory
    {
        public const string GetAll = Base + "/restaurants/{restaurantId}/inventory";
        public const string Create = Base + "/restaurants/{restaurantId}/inventory";
        public const string GetById = Base + "/restaurants/{restaurantId}/inventory/{inventoryItemId}";
        public const string Update = Base + "/restaurants/{restaurantId}/inventory/{inventoryItemId}";
        public const string Activate = Base + "/restaurants/{restaurantId}/inventory/{inventoryItemId}/activate";
        public const string Deactivate = Base + "/restaurants/{restaurantId}/inventory/{inventoryItemId}/deactivate";
        public const string Delete = Base + "/restaurants/{restaurantId}/inventory/{inventoryItemId}";
        public const string CreateMovement = Base + "/restaurants/{restaurantId}/inventory/{inventoryItemId}/movements";
        public const string GetMovementHistory = Base + "/restaurants/{restaurantId}/inventory/{inventoryItemId}/movements";
    }

    public static class Item
    {
        public const string Create = Base + "/restaurants/{restaurantId}/items";
        public const string Update = Base + "/restaurants/{restaurantId}/items/{itemId}";
        public const string Deactivate = Base + "/restaurants/{restaurantId}/items/{itemId}/deactivate";
        public const string Delete = Base + "/restaurants/{restaurantId}/items/{itemId}";
        public const string GetAll = Base + "/items/getAll";
    }

    public static class Lookup
    {
        public const string GetRoles = Base + "/lookups/roles";
        public const string GetItemStatuses = Base + "/lookups/item-statuses";
        public const string GetContractStatuses = Base + "/lookups/contract-statuses";
        public const string GetPaymentPolicies = Base + "/lookups/payment-policies";
        public const string GetUnits = Base + "/lookups/units";
        public const string GetAuditActionsLegacy = Base + "/lookups/actions";
        public const string GetAuditActions = Base + "/lookups/audit-actions";
        public const string GetInventoryMovementTypes = Base + "/lookups/inventory-movement-types";
        public const string GetInventoryMovementTypesLegacy = Base + "/lookups/getInventoryMovementTypes";
        public const string GetOutboxStatuses = Base + "/lookups/outbox-statuses";
        public const string GetNotificationChannels = Base + "/lookups/notification-channels";
    }

    public static class Notification
    {
        public const string GetMine = Base + "/notifications";
        public const string GetUnreadCount = Base + "/notifications/unread-count";
        public const string MarkAsRead = Base + "/notifications/{notificationId:int}/read";
        public const string MarkAllAsRead = Base + "/notifications/read-all";
    }

    public static class Outbox
    {
        public const string GetMessages = Base + "/admin/outbox/messages";
        public const string GetMessage = Base + "/admin/outbox/messages/{id:guid}";
        public const string Retry = Base + "/admin/outbox/messages/{id:guid}/retry";
    }

    public static class PublicRestaurant
    {
        public const string GetRestaurants = Base + "/public/restaurants";
        public const string GetRestaurant = Base + "/public/restaurants/{restaurantId}";
        public const string GetMenu = Base + "/public/restaurants/{restaurantId}/menu";
        public const string GetStaff = Base + "/public/restaurants/{restaurantId}/staff";
        public const string GetTables = Base + "/public/restaurants/{restaurantId}/tables";
        public const string CheckTableAvailability = Base + "/public/restaurants/{restaurantId}/tables/availability";
        public const string GetAvailableTables = Base + "/public/restaurants/{restaurantId}/tables/available";
    }

    public static class Recipe
    {
        public const string GetByItem = Base + "/restaurants/{restaurantId}/items/{itemId}/recipes";
        public const string Create = Base + "/restaurants/{restaurantId}/items/{itemId}/recipes";
        public const string Update = Base + "/restaurants/{restaurantId}/items/{itemId}/recipes/{recipeId}";
        public const string Activate = Base + "/restaurants/{restaurantId}/items/{itemId}/recipes/{recipeId}/activate";
        public const string Deactivate = Base + "/restaurants/{restaurantId}/items/{itemId}/recipes/{recipeId}/deactivate";
        public const string Delete = Base + "/restaurants/{restaurantId}/items/{itemId}/recipes/{recipeId}";
    }

    public static class ReservationArrival
    {
        public const string Get = Base + "/public/reservations/{reservationId:int}/late-arrival";
    }

    public static class ReservationArrivalFlow
    {
        public const string Offer = Base + "/public/reservations/{reservationId:int}/late-arrival/offers";
        public const string Accept = Base + "/public/reservations/{reservationId:int}/late-arrival/accept";
    }

    public static class Reservation
    {
        public const string GetMy = Base + "/public/reservations/my";
        public const string GetById = Base + "/public/reservations/{reservationId:int}";
        public const string GetHistory = Base + "/public/reservations/{reservationId:int}/history";
        public const string Create = Base + "/restaurants/{restaurantId:int}/reservations";
    }

    public static class ReservationFlow
    {
        public const string SubmitPaymentProof = Base + "/restaurants/{restaurantId:int}/reservations/{reservationId:int}/payment-proofs";
        public const string Cancel = Base + "/public/reservations/{reservationId:int}/cancel";
        public const string WaiveDeposit = Base + "/restaurants/{restaurantId:int}/reservations/{reservationId:int}/deposit-waiver";
        public const string SendPaymentInstruction = Base + "/restaurants/{restaurantId:int}/reservations/{reservationId:int}/payment-instructions";
        public const string ApprovePaymentProof = Base + "/restaurants/{restaurantId:int}/reservations/{reservationId:int}/payment-proofs/approve";
        public const string RejectPaymentProof = Base + "/restaurants/{restaurantId:int}/reservations/{reservationId:int}/payment-proofs/reject";
        public const string CheckIn = Base + "/restaurants/{restaurantId:int}/reservations/{reservationId:int}/check-in";
        public const string Complete = Base + "/restaurants/{restaurantId:int}/reservations/{reservationId:int}/complete";
        public const string CancelForRestaurant = Base + "/restaurants/{restaurantId:int}/reservations/{reservationId:int}/cancel";
    }

    public static class ReservationRefund
    {
        public const string GetRefund = Base + "/public/reservations/{reservationId:int}/refund";
        public const string GetRestaurantRefund = Base + "/restaurants/{restaurantId:int}/reservations/{reservationId:int}/refund";
        public const string GetRefundPayoutDetails = Base + "/restaurants/{restaurantId:int}/refunds/{refundId:int}/payout-details";
    }

    public static class ReservationRefundFlow
    {
        public const string RequestRefund = Base + "/public/reservations/{reservationId:int}/refunds";
        public const string SubmitRefundPayoutDetails = Base + "/public/refunds/{refundId:int}/payout-details";
        public const string ConfirmRefundTransfer = Base + "/public/refunds/{refundId:int}/transfers/confirm";
        public const string DisputeRefundTransfer = Base + "/public/refunds/{refundId:int}/transfers/dispute";
        public const string SubmitRefundTransfer = Base + "/restaurants/{restaurantId:int}/refunds/{refundId:int}/transfers";
    }

    public static class RestaurantContract
    {
        public const string Create = Base + "/admin/restaurants/{restaurantId}/contracts";
        public const string Update = Base + "/admin/restaurants/{restaurantId}/contracts/{contractId}";
        public const string GetByRestaurant = Base + "/restaurants/{restaurantId}/contracts";
        public const string GetPagedByRestaurant = Base + "/restaurants/{restaurantId}/contracts/paged";
        public const string GetActive = Base + "/restaurants/{restaurantId}/contracts/active";
        public const string GetActions = Base + "/restaurants/{restaurantId}/contracts/{contractId}/actions";
    }

    public static class RestaurantContractFlow
    {
        public const string SendForSignature = Base + "/admin/restaurants/{restaurantId}/contracts/{contractId}/send-for-signature";
        public const string Approve = Base + "/restaurants/{restaurantId}/contracts/{contractId}/approve";
        public const string Activate = Base + "/admin/restaurants/{restaurantId}/contracts/{contractId}/activate";
        public const string Terminate = Base + "/admin/restaurants/{restaurantId}/contracts/{contractId}/terminate";
    }

    public static class Restaurant
    {
        public const string RegisterRestaurant = Base + "/admin/restaurants";
        public const string UpdateRestaurant = Base + "/admin/restaurants/{restaurantId:int}";
        public const string SetDepositRule = Base + "/admin/restaurants/{restaurantId:int}/deposit-rules/{reservationDate}";
        public const string RemoveDepositRule = Base + "/admin/restaurants/{restaurantId:int}/deposit-rules/{reservationDate}";
        public const string DeactivateRestaurant = Base + "/admin/restaurants/{id}/deactivate";
        public const string GetAllRestaurants = Base + "/restaurants/getAll";
        public const string GetByIdRestaurant = Base + "/restaurant/getById/{id}";
        public const string GeocodeAddress = Base + "/admin/restaurants/geocode";
    }

    public static class RestaurantGroup
    {
        public const string GetAll = Base + "/restaurant-groups";
        public const string Create = Base + "/restaurant-groups";
    }

    public static class RestaurantReservation
    {
        public const string GetList = Base + "/restaurants/{restaurantId:int}/reservations";
        public const string GetById = Base + "/restaurants/{restaurantId:int}/reservations/{reservationId:int}";
        public const string GetHistory = Base + "/restaurants/{restaurantId:int}/reservations/{reservationId:int}/history";
    }

    public static class Table
    {
        public const string CreateTable = Base + "/restaurants/{restaurantId}/tables";
        public const string GetByRestaurant = Base + "/restaurants/{restaurantId}/tables";
        public const string UpdateTable = Base + "/restaurants/{restaurantId}/tables/{tableId}";
        public const string ActivateTable = Base + "/restaurants/{restaurantId}/tables/{tableId}/activate";
        public const string DeactivateTable = Base + "/restaurants/{restaurantId}/tables/{tableId}/deactivate";
        public const string CopyTable = Base + "/restaurants/{restaurantId}/tables/{tableId}/copy";
        public const string DeleteTable = Base + "/restaurants/{restaurantId}/tables/{tableId}";
    }

    public static class User
    {
        public const string Create = Base + "/users";
        public const string Delete = Base + "/users/{userId:int}";
        public const string ActivateStaff = Base + "/restaurants/{restaurantId:int}/staff/{staffId:int}/activate";
        public const string DeactivateStaff = Base + "/restaurants/{restaurantId:int}/staff/{staffId:int}/deactivate";
        public const string UpdateStaff = Base + "/restaurants/{restaurantId:int}/staff/{staffId:int}";
        public const string UpdateRole = Base + "/users/{userId:int}/role";
        public const string GetAll = Base + "/admin/users";
        public const string GetStaff = Base + "/staff/{restaurantId}";
        public const string GetProfile = Base + "/profile";
        public const string UpdateProfile = Base + "/profile";
        public const string GetStaffDetail = Base + "/staff/{restaurantId}/detail/{staffId}";
    }

    public static class UserSession
    {
        public const string GetMySessions = Base + "/user/sessions";
        public const string RevokeSession = Base + "/user/sessions/{sessionId}";
    }

    public static class Workflow
    {
        public const string GetActions = Base + "/workflows/{flowCode}/actions";
    }
}
