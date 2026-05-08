using ECafe.Application.DTOs.User;
using ECafe.Shared.DTOs;

namespace ECafe.Application.Services.User.Abstract
{
    public interface IUserService
    {
        public Task CreateUserAsync(CreateUserRequest request);

        public Task DeleteAsync(int userId);

        public Task UpdateRoleAsync(int userId,int roleId);

        public Task<PaginatedList<GetAllUserResponseDto>> GetAllAsync(int? restaurantId, PaginationFilter filter);

    }
}
