using ECafe.Application.DTOs.Auth;
using ECafe.Application.DTOs.User;
using ECafe.Application.DTOs.User.Staff;
using ECafe.Shared.DTOs;

namespace ECafe.Application.Services.User.Abstract
{
    public interface IUserService
    {
        public Task CreateUserAsync(CreateUserRequest request);

        public Task DeleteAsync(int userId);

        public Task DeactivateStaffAsync(int restaurantId, int staffId);

        public Task<AuthResponseDto> UpdateRoleAsync(int userId, int roleId);

        public Task<PaginatedList<GetAllUserResponseDto>> GetAllAsync(int? restaurantId, PaginationFilter filter);

        public Task<ProfileResponseDto> GetProfileAsync(int userId);

        public Task UpdateProfileAsync(int userId, UpdateProfileRequest request);

        public Task<StaffDetailResponseDto> GetStaffDetailAsync(int restaurantId, int staffId);

    }
}
