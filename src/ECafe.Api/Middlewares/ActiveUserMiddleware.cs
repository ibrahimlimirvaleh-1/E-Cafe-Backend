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
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var userId = GetUserId(context.User);

            if (userId.HasValue && !await userRepository.IsActiveAsync(userId.Value))
                throw new UnauthorizedException(ErrorCode.UserDeactivated);
        }

        await _next(context);
    }

    private static int? GetUserId(ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue("userId")
                    ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);

        return int.TryParse(value, out var userId) ? userId : null;
    }
}
