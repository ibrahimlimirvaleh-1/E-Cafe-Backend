using System.Security.Claims;
using System.Threading.RateLimiting;
using ECafe.Domain.Exceptions;
using Microsoft.AspNetCore.RateLimiting;

namespace ECafe.Api.Security;

public static class RateLimitingServiceCollectionExtensions
{
    public static IServiceCollection AddEcafeRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.ContentType = "application/json";

                TimeSpan? retryAfter = null;
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var metadataRetryAfter))
                    retryAfter = metadataRetryAfter;

                double? retryAfterSeconds = retryAfter.HasValue
                    ? Math.Ceiling(retryAfter.Value.TotalSeconds)
                    : null;

                await context.HttpContext.Response.WriteAsJsonAsync(new
                {
                    statusCode = StatusCodes.Status429TooManyRequests,
                    code = ErrorCode.TooManyRequests.ToString(),
                    message = "Çox sayda sorğu göndərildi. Bir az sonra yenidən cəhd edin.",
                    traceId = context.HttpContext.TraceIdentifier,
                    retryAfterSeconds,
                    timestamp = DateTime.UtcNow
                }, cancellationToken);
            };

            AddFixedWindowPolicy(
                options,
                configuration,
                RateLimitPolicyNames.AuthLogin,
                "RateLimiting:AuthLogin",
                permitLimit: 5,
                windowSeconds: 60);

            AddFixedWindowPolicy(
                options,
                configuration,
                RateLimitPolicyNames.AuthRefresh,
                "RateLimiting:AuthRefresh",
                permitLimit: 10,
                windowSeconds: 60);

            AddFixedWindowPolicy(
                options,
                configuration,
                RateLimitPolicyNames.FileUpload,
                "RateLimiting:FileUpload",
                permitLimit: 20,
                windowSeconds: 300,
                partitionByUserWhenAuthenticated: true);

            AddFixedWindowPolicy(
                options,
                configuration,
                RateLimitPolicyNames.FileDownload,
                "RateLimiting:FileDownload",
                permitLimit: 120,
                windowSeconds: 60);

            AddFixedWindowPolicy(
                options,
                configuration,
                RateLimitPolicyNames.PublicRead,
                "RateLimiting:PublicRead",
                permitLimit: 60,
                windowSeconds: 60);
        });

        return services;
    }

    private static void AddFixedWindowPolicy(
        RateLimiterOptions options,
        IConfiguration configuration,
        string policyName,
        string configurationPath,
        int permitLimit,
        int windowSeconds,
        bool partitionByUserWhenAuthenticated = false)
    {
        var configuredPermitLimit = configuration.GetValue($"{configurationPath}:PermitLimit", permitLimit);
        var configuredWindowSeconds = configuration.GetValue($"{configurationPath}:WindowSeconds", windowSeconds);
        var configuredQueueLimit = configuration.GetValue($"{configurationPath}:QueueLimit", 0);

        options.AddPolicy(policyName, context =>
            RateLimitPartition.GetFixedWindowLimiter(
                GetPartitionKey(context, partitionByUserWhenAuthenticated),
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = configuredPermitLimit,
                    Window = TimeSpan.FromSeconds(configuredWindowSeconds),
                    QueueLimit = configuredQueueLimit,
                    AutoReplenishment = true
                }));
    }

    private static string GetPartitionKey(HttpContext context, bool partitionByUserWhenAuthenticated)
    {
        if (partitionByUserWhenAuthenticated && context.User.Identity?.IsAuthenticated == true)
        {
            var userId = context.User.FindFirstValue("userId")
                         ?? context.User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!string.IsNullOrWhiteSpace(userId))
                return $"user:{userId}";
        }

        return $"ip:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";
    }
}
