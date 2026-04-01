using MediatR;
using Microsoft.AspNetCore.Http;

namespace ECafe.Application.Features.Commands.User
{
    public sealed class CreateUserCommand : IRequest
    {
        public string Name { get; set; } = null!;
        public string Surname { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string Phone { get; set; } = null!;
        public string Password { get; set; } = null!;
        public bool IsActive { get; set; }
        public decimal? Rating { get; set; }
        public IFormFile? Image { get; set; }
        public int RestaurantId { get; set; }
        public int RoleId { get; set; }
    }
}
