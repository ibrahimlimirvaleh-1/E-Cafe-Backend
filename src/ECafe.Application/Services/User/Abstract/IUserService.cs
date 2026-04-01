using ECafe.Application.DTOs.User;

namespace ECafe.Application.Services.User.Abstract
{
    public interface IUserService
    {
        public Task CreateUserAsync(CreateUserRequest request);
    }
}
