using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace ECafe.Api.Swagger;

public sealed class EcafeSwaggerOperationFilter : IOperationFilter
{
    private static readonly IReadOnlyDictionary<string, string> ParameterDescriptions =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = "Resurs identifikatoru.",
            ["restaurantId"] = "Restoran identifikatoru.",
            ["staffId"] = "Staff/ofisiant istifadəçi identifikatoru.",
            ["categoryId"] = "Menyu kateqoriyası identifikatoru.",
            ["statusId"] = "Status identifikatoru.",
            ["pageNumber"] = "Səhifə nömrəsi.",
            ["pageSize"] = "Bir səhifədə qaytarılacaq maksimum qeyd sayı."
        };

    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (context.ApiDescription.ActionDescriptor is not ControllerActionDescriptor action)
            return;

        var key = $"{action.ControllerName}.{action.ActionName}";
        operation.OperationId = $"{action.ControllerName}_{action.ActionName}";
        operation.Tags = new List<OpenApiTag>
        {
            new() { Name = EcafeSwaggerMetadata.GetTagName(action.ControllerName) }
        };

        if (EcafeSwaggerMetadata.Endpoints.TryGetValue(key, out var endpoint))
        {
            operation.Summary = endpoint.Summary;
            operation.Description = endpoint.Description;
        }
        else
        {
            operation.Summary ??= $"{action.ControllerName}: {action.ActionName}";
            operation.Description ??= $"{action.ControllerName} modulu üçün {action.ActionName} əməliyyatı.";
        }

        ImproveParameters(operation);
        ImproveResponses(operation, action);
    }

    private static void ImproveParameters(OpenApiOperation operation)
    {
        if (operation.Parameters is null)
            return;

        foreach (var parameter in operation.Parameters)
        {
            if (ParameterDescriptions.TryGetValue(parameter.Name, out var description))
                parameter.Description = description;
        }
    }

    private static void ImproveResponses(OpenApiOperation operation, ControllerActionDescriptor action)
    {
        if (operation.Responses.TryGetValue("200", out var okResponse))
            okResponse.Description = "Uğurlu cavab.";

        if (operation.Responses.TryGetValue("201", out var createdResponse))
            createdResponse.Description = "Resurs yaradıldı.";

        operation.Responses.TryAdd("400", new OpenApiResponse
        {
            Description = "Sorğu validasiya və ya biznes qaydasına uyğun deyil."
        });

        operation.Responses.TryAdd("500", new OpenApiResponse
        {
            Description = "Server xətası. Cavabda traceId varsa loglarda həmin izlə axtarmaq olar."
        });

        var requiresAuth = action.MethodInfo.GetCustomAttributes(true).OfType<AuthorizeAttribute>().Any()
            || action.ControllerTypeInfo.GetCustomAttributes(true).OfType<AuthorizeAttribute>().Any();

        if (!requiresAuth)
            return;

        operation.Responses.TryAdd("401", new OpenApiResponse
        {
            Description = "JWT token yoxdur və ya etibarsızdır."
        });

        operation.Responses.TryAdd("403", new OpenApiResponse
        {
            Description = "Token var, amma bu əməliyyat üçün icazə yoxdur."
        });
    }
}
