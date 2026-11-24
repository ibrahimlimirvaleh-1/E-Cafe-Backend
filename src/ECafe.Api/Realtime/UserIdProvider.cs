using System.Security.Claims;
using ECafe.Shared.Security;
using Microsoft.AspNetCore.SignalR;

namespace ECafe.Api.Realtime;

public sealed class UserIdProvider : IUserIdProvider
{
    /// <summary>SignalR istifadəçisini əvvəlki userId və NameIdentifier ardıcıllığı ilə müəyyən edir.</summary>
    public string? GetUserId(HubConnectionContext connection)
        => connection.User?.FindFirstValue(AuthenticationClaimTypes.UserId) ??
           connection.User?.FindFirstValue(ClaimTypes.NameIdentifier);
}
