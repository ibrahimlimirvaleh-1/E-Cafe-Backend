using ECafe.Application.Services.User.Abstract;
using MediatR;

namespace ECafe.Application.Features.Commands.User.Create
{
    public class CreateUserCommandHandler : IRequestHandler<CreateUserCommand>
    {
        private readonly IUserService _userService;

        public CreateUserCommandHandler(IUserService userService)
        {
            _userService = userService;
        }

        public async Task Handle(CreateUserCommand request, CancellationToken cancellationToken)
        {
            await _userService.CreateUserAsync(request);
        }
    }
}
