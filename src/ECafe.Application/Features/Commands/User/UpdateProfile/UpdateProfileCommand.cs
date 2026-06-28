using ECafe.Application.DTOs.User;
using MediatR;

namespace ECafe.Application.Features.Commands.User.UpdateProfile
{
    public class UpdateProfileCommand : UpdateProfileRequest, IRequest
    {
        public int UserId { get; set; }
    }
}
