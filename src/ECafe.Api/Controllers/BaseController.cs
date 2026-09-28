using ECafe.Api.Routes;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ECafe.Api.Controllers;

[ApiController]
[Route(ApiRoutes.V1)]
public abstract class BaseController : ControllerBase
{
    private IMediator? _mediator;

    protected IMediator Mediator =>
        _mediator ??= HttpContext.RequestServices.GetRequiredService<IMediator>();
}
