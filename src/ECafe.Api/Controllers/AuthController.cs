using ECafe.Application.Features.Commands.Auth.Login;
using ECafe.Application.Features.Commands.Auth.Register;
using Microsoft.AspNetCore.Mvc;

namespace ECafe.Api.Controllers
{

    public class AuthController : BaseController
    {
        [HttpPost("api/user/login")]
        public async Task<IActionResult> Login([FromForm] LoginUserCommand command)
        => Ok(await Mediator.Send(command));

        [HttpPost("api/user/register")]
        public async Task<IActionResult> Register([FromForm] RegisterUserCommand command)
            => Ok(await Mediator.Send(command));

    }
}
