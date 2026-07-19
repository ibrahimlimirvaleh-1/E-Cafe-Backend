using ECafe.Application.Features.Commands.Auth.Login;
using ECafe.Application.Features.Commands.Auth.Refresh;
using ECafe.Application.Features.Commands.Auth.Register;
using Microsoft.AspNetCore.Mvc;

namespace ECafe.Api.Controllers
{

    public class AuthController : BaseController
    {
        [HttpPost("api/v1/user/login")]
        [Consumes("application/json")]
        public async Task<IActionResult> Login([FromBody] LoginUserCommand command)
            => Ok(await Mediator.Send(command));

        [HttpPost("api/v1/user/login")]
        [Consumes("multipart/form-data", "application/x-www-form-urlencoded")]
        public async Task<IActionResult> LoginForm([FromForm] LoginUserCommand command)
            => Ok(await Mediator.Send(command));

        [HttpPost("api/v1/user/register")]
        public async Task<IActionResult> Register([FromForm] RegisterUserCommand command)
            => Ok(await Mediator.Send(command));

        [HttpPost("api/v1/user/refresh")]
        public async Task<IActionResult> Refresh([FromBody] RefreshTokenCommand command)
            => Ok(await Mediator.Send(command));

    }
}
