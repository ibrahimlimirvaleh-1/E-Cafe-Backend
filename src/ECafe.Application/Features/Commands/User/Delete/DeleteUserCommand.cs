using MediatR;

namespace ECafe.Application.Features.Commands.User.Delete
{
    public class DeleteUserCommand : IRequest
    {
        public int Id { get; set; }
    }
}
