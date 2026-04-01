using AutoMapper;
using ECafe.Application.DTOs.User;
using ECafe.Application.Services.User.Abstract;
using MediatR;

namespace ECafe.Application.Features.Commands.User
{
    public class CreateUserCommandHandler : IRequestHandler<CreateUserCommand>
    {
        private readonly IUserService _userService;
        private readonly IMapper _mapper;

        public CreateUserCommandHandler(IUserService userService, IMapper mapper)
        {
            _userService = userService;
            _mapper = mapper;
        }
        public async Task Handle(CreateUserCommand request, CancellationToken cancellationToken)
        {
            var dto = new CreateUserRequest
            {
                Name = request.Name,
                Surname = request.Surname,
                Email = request.Email,
                Phone = request.Phone,
                Password = request.Password,
                IsActive = request.IsActive,
                Rating = request.Rating,
                Image = request.Image,
                RestaurantId = request.RestaurantId,
                RoleId = request.RoleId
            };
            await _userService.CreateUserAsync(dto);
        }
    }
}
