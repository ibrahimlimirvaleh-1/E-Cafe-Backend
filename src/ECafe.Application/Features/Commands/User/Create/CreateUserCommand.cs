using ECafe.Application.DTOs.User;
using MediatR;

namespace ECafe.Application.Features.Commands.User.Create
{
    public sealed class CreateUserCommand : CreateUserRequest, IRequest
    {
    }
}
