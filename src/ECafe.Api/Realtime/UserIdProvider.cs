using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;

namespace ECafe.Api.Realtime;

public sealed class UserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection)
        => connection.User?.FindFirstValue("userId") ??
           connection.User?.FindFirstValue(ClaimTypes.NameIdentifier);
}
