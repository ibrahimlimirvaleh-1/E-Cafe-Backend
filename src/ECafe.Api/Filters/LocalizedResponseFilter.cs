using System.Collections;
using System.Reflection;
using ECafe.Application.DTOs.Notification;
using ECafe.Application.DTOs.Reservation;
using ECafe.Application.DTOs.RestaurantContract;
using ECafe.Application.Services.Localization.Abstract;
using ECafe.Application.Services.ContentLocalization.Abstract;
using ECafe.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using ItemResponse = ECafe.Application.DTOs.Item.ItemDto;

namespace ECafe.Api.Filters;

public sealed class LocalizedResponseFilter(ITextLocalizer text, IContentTranslationReader? content = null) : IAsyncResultFilter
{
    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        if (context.Result is ObjectResult { Value: not null } result && (result.StatusCode ?? 200) < 400)
        {
            if (result.Value is IEnumerable collection && result.Value is not string) result.Value = collection.Cast<object?>().ToList();
            Localize(result.Value, new HashSet<object>(ReferenceEqualityComparer.Instance), 0);
            if (content != null)
            {
                var route = context.RouteData.Values;
                var rawId = route.GetValueOrDefault("restaurantId") ?? route.GetValueOrDefault("id");
                int? restaurantId = int.TryParse(rawId?.ToString(), out var id) ? id : null;
                var unitLookup = route.GetValueOrDefault("controller")?.ToString() == "Lookup" && route.GetValueOrDefault("action")?.ToString() == "GetUnits";
                await content.LocalizeAsync(result.Value, context.HttpContext.RequestAborted, new ContentReadContext(restaurantId, unitLookup));
            }
        }
        await next();
    }

    /// <summary>Yalnız API görünüşlərinin sistem mətn sahələrini lokallaşdırır; domen obyektinə, istifadəçi adına, restoran və menyu məzmununa toxunmur.</summary>
    private void Localize(object value, HashSet<object> visited, int depth)
    {
        if (depth > 12 || value is string || !visited.Add(value)) return;
        if (value is IEnumerable collection)
        {
            foreach (var item in collection) if (item != null) Localize(item, visited, depth + 1);
            return;
        }
        var type = value.GetType();
        var isPage = type.IsGenericType && type.GetGenericTypeDefinition() == typeof(PaginatedList<>);
        if ((!isPage && type.Namespace?.StartsWith("ECafe.Application.DTOs", StringComparison.Ordinal) != true) || value is NotificationResponse) return;
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(property => property.GetIndexParameters().Length == 0 && property.CanRead))
        {
            var field = property.GetValue(value);
            if (field is string message && property.CanWrite && IsSystemText(type, property.Name))
                property.SetValue(value, text.TranslateLiteral(message));
            else if (field != null && field is not string && !property.PropertyType.IsValueType)
                Localize(field, visited, depth + 1);
        }
    }

    /// <summary>İcazəli sistem, məhsul, müqavilə və refund görünüş sahələrini seçir; xam status kodunu, ləğv səbəbini və sərbəst tarixçə qeydini tərcümə etmir.</summary>
    private static bool IsSystemText(Type type, string property)
        => property == "Message" ||
            type == typeof(RestaurantContractResponse) && property == nameof(RestaurantContractResponse.StatusName) ||
            type == typeof(ItemResponse) && property == nameof(ItemResponse.StatusName) ||
            type.Name == "WorkflowActionResponse" && property == "Label" ||
            type.Name is "LookupItemResponse" or "RoleLookupItemResponse" && property == "Name" ||
            type == typeof(ReservationRefundResponse) && property is "StatusLabel" or "CustomerNextStep" or "EligibilityReason" ||
            type == typeof(ReservationRefundStatusHistoryResponse) && property is "FromStatusLabel" or "ToStatusLabel" or "ReasonLabel";
}
