using ECafe.Application.Features.Commands;
using Microsoft.AspNetCore.Mvc;

namespace ECafe.Api.Controllers
{

    public class AuthController : BaseController
    {
        [HttpPost("api/user/login")]
        public async Task<IActionResult> Login([FromForm] LoginUserCommand command)
        => Ok(await Mediator.Send(command));

    }
}
