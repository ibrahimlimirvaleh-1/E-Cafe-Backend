using ECafe.Application.Features.Commands.Auth.Login;
using ECafe.Application.Features.Commands.Auth.Refresh;
using ECafe.Application.Features.Commands.Auth.Register;
using ECafe.Api.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ECafe.Api.Controllers
{

    public class AuthController : BaseController
    {
        [HttpPost("api/v1/user/login")]
        [Consumes("application/json")]
        [EnableRateLimiting(RateLimitPolicyNames.AuthLogin)]
        public async Task<IActionResult> Login([FromBody] LoginUserCommand command)
            => Ok(await Mediator.Send(command));


        [HttpPost("api/v1/user/register")]
        [EnableRateLimiting(RateLimitPolicyNames.AuthLogin)]
        public async Task<IActionResult> Register([FromForm] RegisterUserCommand command)
            => Ok(await Mediator.Send(command));


        [HttpPost("api/v1/user/refresh")]
        [EnableRateLimiting(RateLimitPolicyNames.AuthRefresh)]
        public async Task<IActionResult> Refresh([FromBody] RefreshTokenCommand command)
            => Ok(await Mediator.Send(command));


    }
}
