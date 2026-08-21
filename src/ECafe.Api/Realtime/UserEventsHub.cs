using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ECafe.Api.Realtime;

[Authorize]
public sealed class UserEventsHub : Hub
{
}
