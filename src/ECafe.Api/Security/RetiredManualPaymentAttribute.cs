using ECafe.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ECafe.Api.Security;

[AttributeUsage(AttributeTargets.Method)]
public sealed class RetiredManualPaymentAttribute : Attribute, IResourceFilter
{
    /// <summary>Model binding və fayl yüklənməsindən əvvəl köhnə rekvizit/çek endpointini bağlayır; request body-ni emal etmir.</summary>
    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        var messages = context.HttpContext.RequestServices.GetRequiredService<IErrorMessageProvider>();
        context.Result = new ObjectResult(new
        {
            statusCode = StatusCodes.Status410Gone, code = nameof(ErrorCode.ManualPaymentRetired),
            message = messages.GetMessage(ErrorCode.ManualPaymentRetired)
        }) { StatusCode = StatusCodes.Status410Gone };
    }

    /// <summary>Retired endpoint heç bir əməliyyat icra etmədiyi üçün sonradan məlumat dəyişmir.</summary>
    public void OnResourceExecuted(ResourceExecutedContext context) { }
}
