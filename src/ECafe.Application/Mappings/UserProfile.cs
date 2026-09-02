using AutoMapper;
using ECafe.Application.Common.Validation;
using ECafe.Application.DTOs.User;
using ECafe.Application.DTOs.User.Staff;
using ECafe.Domain.Entities;

namespace ECafe.Application.Mappings
{
    public class UserProfile : Profile
    {
        public UserProfile()
        {
            CreateMap<CreateUserRequest, User>()
                .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name.Trim()))
                .ForMember(dest => dest.Surname, opt => opt.MapFrom(src => src.Surname.Trim()))
                .ForMember(dest => dest.Email, opt => opt.MapFrom(src => src.Email.Trim().ToLowerInvariant()))
                .ForMember(dest => dest.Phone, opt => opt.MapFrom(src => PhoneNumberValidationExtensions.NormalizeAzerbaijanPhoneNumber(src.Phone)))
                .ForMember(dest => dest.FileId, opt => opt.Ignore())
                .ForMember(dest => dest.Password, opt => opt.Ignore())
                .ForMember(dest => dest.PasswordSetAt, opt => opt.Ignore())
                .ForMember(dest => dest.File, opt => opt.Ignore())
                .ForMember(dest => dest.UserRestaurants, opt => opt.MapFrom(src => new List<UserRestaurant>
                {
                    new()
                    {
                        RestaurantId = src.RestaurantId,
                        RoleId = src.RoleId,
                        IsActive = true,
                        ServiceFeePercent = src.ServiceFeePercent
                    }
                }));

            CreateMap<UpdateProfileRequest, User>()
                .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name.Trim()))
                .ForMember(dest => dest.Surname, opt => opt.MapFrom(src => src.Surname.Trim()))
                .ForMember(dest => dest.Email, opt => opt.MapFrom(src => src.Email.Trim().ToLowerInvariant()))
                .ForMember(dest => dest.Phone, opt => opt.MapFrom(src => PhoneNumberValidationExtensions.NormalizeAzerbaijanPhoneNumber(src.Phone)))
                .ForMember(dest => dest.FileId, opt => opt.Ignore())
                .ForMember(dest => dest.File, opt => opt.Ignore());

            CreateMap<UpdateStaffRequest, User>()
                .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name.Trim()))
                .ForMember(dest => dest.Surname, opt => opt.MapFrom(src => src.Surname.Trim()))
                .ForMember(dest => dest.Email, opt => opt.MapFrom(src => src.Email.Trim().ToLowerInvariant()))
                .ForMember(dest => dest.Phone, opt => opt.MapFrom(src => PhoneNumberValidationExtensions.NormalizeAzerbaijanPhoneNumber(src.Phone)))
                .ForMember(dest => dest.FileId, opt => opt.Ignore())
                .ForMember(dest => dest.File, opt => opt.Ignore())
                .ForMember(dest => dest.Password, opt => opt.Ignore())
                .ForMember(dest => dest.PasswordSetAt, opt => opt.Ignore())
                .ForMember(dest => dest.RoleId, opt => opt.Ignore())
                .ForMember(dest => dest.Role, opt => opt.Ignore())
                .ForMember(dest => dest.UserRestaurants, opt => opt.Ignore());

            CreateMap<Role, RoleDto>();

            CreateMap<User, GetAllUserResponseDto>();

            CreateMap<UserRestaurant, GetAllUserResponseDto>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.User.Id))
                .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.User.Name))
                .ForMember(dest => dest.Surname, opt => opt.MapFrom(src => src.User.Surname))
                .ForMember(dest => dest.IsActive, opt => opt.MapFrom(src => src.IsActive && src.User.IsActive))
                .ForMember(dest => dest.Rating, opt => opt.MapFrom(src => src.User.Rating))
                .ForMember(dest => dest.Role, opt => opt.MapFrom(src => src.Role));

            CreateMap<User, ProfileResponseDto>()
                .ForMember(dest => dest.Role, opt => opt.MapFrom(src => src.Role.Name))
                .ForMember(dest => dest.RestaurantId, opt => opt.MapFrom(src => src.UserRestaurants
                    .Where(ur => ur.IsActive)
                    .Select(ur => (int?)ur.RestaurantId)
                    .FirstOrDefault()))
                .ForMember(dest => dest.RestaurantName, opt => opt.MapFrom(src => src.UserRestaurants
                    .Where(ur => ur.IsActive)
                    .Select(ur => ur.Restaurant.Name)
                    .FirstOrDefault()))
                .ForMember(dest => dest.FileUrl, opt => opt.Ignore());

            CreateMap<User, StaffDetailResponseDto>()
                .ForMember(dest => dest.RoleId, opt => opt.MapFrom(src => src.RoleId))
                .ForMember(dest => dest.Role, opt => opt.MapFrom(src => src.Role.Name))
                .ForMember(dest => dest.FileUrl, opt => opt.Ignore());

            CreateMap<UserRestaurant, StaffPublicResponseDto>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.User.Id))
                .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.User.Name))
                .ForMember(dest => dest.Surname, opt => opt.MapFrom(src => src.User.Surname))
                .ForMember(dest => dest.Rating, opt => opt.MapFrom(src => src.User.Rating))
                .ForMember(dest => dest.RoleId, opt => opt.MapFrom(src => src.RoleId))
                .ForMember(dest => dest.Role, opt => opt.MapFrom(src => src.Role.Name))
                .ForMember(dest => dest.ServiceFeePercent, opt => opt.MapFrom(src => src.ServiceFeePercent))
                .ForMember(dest => dest.FileUrl, opt => opt.Ignore());

            CreateMap<UserRestaurant, StaffDetailResponseDto>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.User.Id))
                .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.User.Name))
                .ForMember(dest => dest.Surname, opt => opt.MapFrom(src => src.User.Surname))
                .ForMember(dest => dest.Rating, opt => opt.MapFrom(src => src.User.Rating))
                .ForMember(dest => dest.RoleId, opt => opt.MapFrom(src => src.RoleId))
                .ForMember(dest => dest.Role, opt => opt.MapFrom(src => src.Role.Name))
                .ForMember(dest => dest.Email, opt => opt.MapFrom(src => src.User.Email))
                .ForMember(dest => dest.Phone, opt => opt.MapFrom(src => src.User.Phone))
                .ForMember(dest => dest.IsActive, opt => opt.MapFrom(src => src.IsActive && src.User.IsActive))
                .ForMember(dest => dest.ServiceFeePercent, opt => opt.MapFrom(src => src.ServiceFeePercent))
                .ForMember(dest => dest.FileUrl, opt => opt.Ignore());
        }
    }
}
