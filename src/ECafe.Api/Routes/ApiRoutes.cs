global using ECafe.Api.Routes;

namespace ECafe.Api.Routes;

internal static class ApiRoutes
{
    public const string V1 = "api/v1";
    public const string UserCookiePath = "/" + V1 + "/user";
    public const string UserRefreshPath = UserCookiePath + "/refresh";
    public const string UserEventsHub = "/hubs/user-events";
    public const string HealthLive = "/health/live";
    public const string HealthReady = "/health/ready";
    public const string SwaggerUi = "swagger";
    public const string SwaggerJson = "/" + SwaggerUi + "/v1/swagger.json";
    public const string SwaggerStylesheet = "/swagger-ui/ecafe-swagger.css?v=20260714-3";
    public const string SwaggerJavascript = "/swagger-ui/ecafe-swagger.js?v=20260714-3";

    public static class AuditLog
    {
        public const string GetRestaurantTimeline = "restaurants/{restaurantId}/audit-logs";
    }

    public static class Auth
    {
        public const string Login = "user/login";
        public const string Register = "user/register";
        public const string SetPassword = "user/set-password";
        public const string ForgotPassword = "user/forgot-password";
        public const string ResetPassword = "user/reset-password";
        public const string Refresh = "user/refresh";
        public const string Logout = "user/logout";
        public const string LogoutAll = "user/logout-all";
    }

    public static class Category
    {
        public const string GetAll = "category/{restaurantId}";
        public const string Create = "restaurants/{restaurantId}/categories";
        public const string Update = "restaurants/{restaurantId}/categories/{categoryId}";
        public const string Activate = "restaurants/{restaurantId}/categories/{categoryId}/activate";
        public const string Deactivate = "restaurants/{restaurantId}/categories/{categoryId}/deactivate";
        public const string Delete = "restaurants/{restaurantId}/categories/{categoryId}";
    }

    public static class DeveloperNotificationTest
    {
        public const string SendEmail = "developer/test/email";
        public const string SendSms = "developer/test/sms";
        public const string GetSmsBalance = "developer/test/sms/balance";
        public const string GetSmsStatus = "developer/test/sms/status/{messageId}";
    }

    public static class File
    {
        public const string Upload = "file/upload";
        public const string Delete = "file/{fileId:int}";
        public const string GetById = "file/{fileId:int}";
        public const string View = "files/{fileId:int}/view";
        public const string Download = "files/{fileId:int}/download";
        public const string GetFile = "file/getFile";
    }

    public static class Inventory
    {
        public const string GetAll = "restaurants/{restaurantId}/inventory";
        public const string Create = "restaurants/{restaurantId}/inventory";
        public const string GetById = "restaurants/{restaurantId}/inventory/{inventoryItemId}";
        public const string Update = "restaurants/{restaurantId}/inventory/{inventoryItemId}";
        public const string Activate = "restaurants/{restaurantId}/inventory/{inventoryItemId}/activate";
        public const string Deactivate = "restaurants/{restaurantId}/inventory/{inventoryItemId}/deactivate";
        public const string Delete = "restaurants/{restaurantId}/inventory/{inventoryItemId}";
        public const string CreateMovement = "restaurants/{restaurantId}/inventory/{inventoryItemId}/movements";
        public const string GetMovementHistory = "restaurants/{restaurantId}/inventory/{inventoryItemId}/movements";
    }

    public static class Item
    {
        public const string Create = "restaurants/{restaurantId}/items";
        public const string Update = "restaurants/{restaurantId}/items/{itemId}";
        public const string Deactivate = "restaurants/{restaurantId}/items/{itemId}/deactivate";
        public const string Delete = "restaurants/{restaurantId}/items/{itemId}";
        public const string GetAll = "items/getAll";
    }

    public static class Lookup
    {
        public const string GetRoles = "lookups/roles";
        public const string GetItemStatuses = "lookups/item-statuses";
        public const string GetContractStatuses = "lookups/contract-statuses";
        public const string GetPaymentPolicies = "lookups/payment-policies";
        public const string GetUnits = "lookups/units";
        public const string GetAuditActions = "lookups/actions";
        public const string GetAuditActions2 = "lookups/audit-actions";
        public const string GetInventoryMovementTypes = "lookups/inventory-movement-types";
        public const string GetInventoryMovementTypes2 = "lookups/getInventoryMovementTypes";
        public const string GetOutboxStatuses = "lookups/outbox-statuses";
        public const string GetNotificationChannels = "lookups/notification-channels";
    }

    public static class Notification
    {
        public const string GetMine = "notifications";
        public const string GetUnreadCount = "notifications/unread-count";
        public const string MarkAsRead = "notifications/{notificationId:int}/read";
        public const string MarkAllAsRead = "notifications/read-all";
    }

    public static class Outbox
    {
        public const string GetMessages = "admin/outbox/messages";
        public const string GetMessage = "admin/outbox/messages/{id:guid}";
        public const string Retry = "admin/outbox/messages/{id:guid}/retry";
    }

    public static class PublicRestaurant
    {
        public const string GetRestaurants = "public/restaurants";
        public const string GetRestaurant = "public/restaurants/{restaurantId}";
        public const string GetMenu = "public/restaurants/{restaurantId}/menu";
        public const string GetStaff = "public/restaurants/{restaurantId}/staff";
        public const string GetTables = "public/restaurants/{restaurantId}/tables";
        public const string CheckTableAvailability = "public/restaurants/{restaurantId}/tables/availability";
        public const string GetAvailableTables = "public/restaurants/{restaurantId}/tables/available";
    }

    public static class Recipe
    {
        public const string GetByItem = "restaurants/{restaurantId}/items/{itemId}/recipes";
        public const string Create = "restaurants/{restaurantId}/items/{itemId}/recipes";
        public const string Update = "restaurants/{restaurantId}/items/{itemId}/recipes/{recipeId}";
        public const string Activate = "restaurants/{restaurantId}/items/{itemId}/recipes/{recipeId}/activate";
        public const string Deactivate = "restaurants/{restaurantId}/items/{itemId}/recipes/{recipeId}/deactivate";
        public const string Delete = "restaurants/{restaurantId}/items/{itemId}/recipes/{recipeId}";
    }

    public static class Reservation
    {
        public const string GetMy = "public/reservations/my";
        public const string GetById = "public/reservations/{reservationId:int}";
        public const string GetHistory = "public/reservations/{reservationId:int}/history";
        public const string Create = "restaurants/{restaurantId:int}/reservations";
        public const string SubmitPaymentProof = "restaurants/{restaurantId:int}/reservations/{reservationId:int}/payment-proofs";
        public const string Cancel = "public/reservations/{reservationId:int}/cancel";
        public const string GetRefund = "public/reservations/{reservationId:int}/refund";
        public const string RequestRefund = "public/reservations/{reservationId:int}/refunds";
        public const string SubmitRefundPayoutDetails = "public/refunds/{refundId:int}/payout-details";
    }

    public static class RestaurantContract
    {
        public const string Create = "admin/restaurants/{restaurantId}/contracts";
        public const string Update = "admin/restaurants/{restaurantId}/contracts/{contractId}";
        public const string GetByRestaurant = "restaurants/{restaurantId}/contracts";
        public const string GetPagedByRestaurant = "restaurants/{restaurantId}/contracts/paged";
        public const string GetActive = "restaurants/{restaurantId}/contracts/active";
        public const string GetActions = "restaurants/{restaurantId}/contracts/{contractId}/actions";
        public const string SendForSignature = "admin/restaurants/{restaurantId}/contracts/{contractId}/send-for-signature";
        public const string Approve = "restaurants/{restaurantId}/contracts/{contractId}/approve";
        public const string Activate = "admin/restaurants/{restaurantId}/contracts/{contractId}/activate";
        public const string Terminate = "admin/restaurants/{restaurantId}/contracts/{contractId}/terminate";
    }

    public static class Restaurant
    {
        public const string RegisterRestaurant = "admin/restaurants";
        public const string UpdateRestaurant = "admin/restaurants/{id}";
        public const string DeactivateRestaurant = "admin/restaurants/{id}/deactivate";
        public const string GetAllRestaurants = "restaurants/getAll";
        public const string GetByIdRestaurant = "restaurant/getById/{id}";
        public const string GeocodeAddress = "admin/restaurants/geocode";
    }

    public static class RestaurantGroup
    {
        public const string GetAll = "restaurant-groups";
        public const string Create = "restaurant-groups";
    }

    public static class RestaurantReservation
    {
        public const string GetList = "restaurants/{restaurantId:int}/reservations";
        public const string GetById = "restaurants/{restaurantId:int}/reservations/{reservationId:int}";
        public const string GetHistory = "restaurants/{restaurantId:int}/reservations/{reservationId:int}/history";
        public const string SendPaymentInstruction = "restaurants/{restaurantId:int}/reservations/{reservationId:int}/payment-instructions";
        public const string ApprovePaymentProof = "restaurants/{restaurantId:int}/reservations/{reservationId:int}/payment-proofs/approve";
        public const string RejectPaymentProof = "restaurants/{restaurantId:int}/reservations/{reservationId:int}/payment-proofs/reject";
        public const string CheckIn = "restaurants/{restaurantId:int}/reservations/{reservationId:int}/check-in";
        public const string Complete = "restaurants/{restaurantId:int}/reservations/{reservationId:int}/complete";
        public const string Cancel = "restaurants/{restaurantId:int}/reservations/{reservationId:int}/cancel";
        public const string GetRefund = "restaurants/{restaurantId:int}/reservations/{reservationId:int}/refund";
        public const string GetRefundPayoutDetails = "restaurants/{restaurantId:int}/refunds/{refundId:int}/payout-details";
        public const string SubmitRefundTransfer = "restaurants/{restaurantId:int}/refunds/{refundId:int}/transfers";
    }

    public static class Table
    {
        public const string CreateTable = "restaurants/{restaurantId}/tables";
        public const string GetByRestaurant = "restaurants/{restaurantId}/tables";
        public const string UpdateTable = "restaurants/{restaurantId}/tables/{tableId}";
        public const string ActivateTable = "restaurants/{restaurantId}/tables/{tableId}/activate";
        public const string DeactivateTable = "restaurants/{restaurantId}/tables/{tableId}/deactivate";
        public const string CopyTable = "restaurants/{restaurantId}/tables/{tableId}/copy";
        public const string DeleteTable = "restaurants/{restaurantId}/tables/{tableId}";
    }

    public static class User
    {
        public const string Create = "users";
        public const string Delete = "users/{userId:int}";
        public const string ActivateStaff = "restaurants/{restaurantId:int}/staff/{staffId:int}/activate";
        public const string DeactivateStaff = "restaurants/{restaurantId:int}/staff/{staffId:int}/deactivate";
        public const string UpdateStaff = "restaurants/{restaurantId:int}/staff/{staffId:int}";
        public const string UpdateRole = "users/{userId:int}/role";
        public const string GetAll = "admin/users";
        public const string GetStaff = "staff/{restaurantId}";
        public const string GetProfile = "profile";
        public const string UpdateProfile = "profile";
        public const string GetStaffDetail = "staff/{restaurantId}/detail/{staffId}";
    }

    public static class UserSession
    {
        public const string GetMySessions = "user/sessions";
        public const string RevokeSession = "user/sessions/{sessionId}";
    }

    public static class Workflow
    {
        public const string GetActions = "workflows/{flowCode}/actions";
    }
}
