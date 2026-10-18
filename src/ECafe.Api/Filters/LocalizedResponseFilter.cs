using System.Collections;
using System.Reflection;
using ECafe.Application.DTOs.Notification;
using ECafe.Application.Services.Localization.Abstract;
using ECafe.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ECafe.Api.Filters;

public sealed class LocalizedResponseFilter(ITextLocalizer text) : IAsyncResultFilter
{
    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        if (context.Result is ObjectResult { Value: not null } result && (result.StatusCode ?? 200) < 400)
        {
            if (result.Value is IEnumerable collection && result.Value is not string) result.Value = collection.Cast<object?>().ToList();
            Localize(result.Value, new HashSet<object>(ReferenceEqualityComparer.Instance), 0);
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

    /// <summary>İcazəli görünüş sahələrini açıq siyahı ilə seçir; Name, Note, Reason və Description kimi istifadəçi mətnlərini ümumi qayda ilə çevirmir.</summary>
    private static bool IsSystemText(Type type, string property)
        => property == "Message" ||
            type.Name == "WorkflowActionResponse" && property == "Label" ||
            type.Name is "LookupItemResponse" or "RoleLookupItemResponse" && property == "Name";
}
