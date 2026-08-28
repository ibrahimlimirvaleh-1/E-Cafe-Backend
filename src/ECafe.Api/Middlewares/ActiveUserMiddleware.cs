using System.Security.Claims;
using ECafe.Application.Repositories.User;
using ECafe.Domain.Exceptions;

namespace ECafe.Api.Middlewares;

public sealed class ActiveUserMiddleware
{
    private readonly RequestDelegate _next;

    public ActiveUserMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task Invoke(HttpContext context, IUserRepository userRepository)
    {
        if (!ShouldSkipActiveUserCheck(context) && context.User.Identity?.IsAuthenticated == true)
        {
            var userId = GetUserId(context.User);

            if (userId.HasValue)
            {
                var sessionVersion = GetSessionVersion(context.User);
                if (!sessionVersion.HasValue)
                    throw new UnauthorizedException(ErrorCode.SessionInvalid);

                var sessionState = await userRepository.GetSessionStateAsync(userId.Value);
                if (sessionState is null || !sessionState.Value.IsActive)
                    throw new UnauthorizedException(ErrorCode.UserDeactivated);

                if (sessionState.Value.SessionVersion != sessionVersion.Value)
                    throw new UnauthorizedException(ErrorCode.SessionInvalid);
            }
        }

        await _next(context);
    }

    private static bool ShouldSkipActiveUserCheck(HttpContext context)
        => context.Request.Path.Equals("/api/v1/user/refresh", StringComparison.OrdinalIgnoreCase);

    private static int? GetUserId(ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue("userId")
                    ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);

        return int.TryParse(value, out var userId) ? userId : null;
    }

    private static int? GetSessionVersion(ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue("sessionVersion");
        return int.TryParse(value, out var sessionVersion) ? sessionVersion : null;
    }
}
