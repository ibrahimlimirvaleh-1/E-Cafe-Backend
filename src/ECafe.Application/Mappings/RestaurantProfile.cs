using AutoMapper;
using ECafe.Application.DTOs.Restaurant;
using ECafe.Application.DTOs.Restaurant.Public;
using ECafe.Domain.Entities;

namespace ECafe.Application.Mappings
{
    public class RestaurantProfile : Profile
    {
        public RestaurantProfile()
        {
            CreateMap<Restaurant, RestaurantDetailDto>()
                .ForMember(dest => dest.RestaurantGroupName, opt => opt.MapFrom(src => src.RestaurantGroup == null ? null : src.RestaurantGroup.Name));

            CreateMap<Table, TableDto>();

            CreateMap<Table, PublicTableDto>();

            CreateMap<Category, CategoryDto>();

            CreateMap<Category, PublicMenuCategoryDto>()
                .ForMember(dest => dest.Items, opt => opt.MapFrom(src => src.Items
                    .Where(item => item.IsActive && item.IsAvailable)
                    .OrderBy(item => item.Name)));

            CreateMap<Item, ItemDto>();

            CreateMap<Item, PublicMenuItemDto>()
                .ForMember(dest => dest.FileUrl, opt => opt.Ignore());

            CreateMap<Role, RoleDto>();

            CreateMap<User, UserDto>()
                .ForMember(dest => dest.Role, opt => opt.MapFrom(src => src.Role));

            CreateMap<UserRestaurant, UserDto>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.User.Id))
                .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.User.Name))
                .ForMember(dest => dest.Surname, opt => opt.MapFrom(src => src.User.Surname))
                .ForMember(dest => dest.Email, opt => opt.MapFrom(src => src.User.Email))
                .ForMember(dest => dest.Phone, opt => opt.MapFrom(src => src.User.Phone))
                .ForMember(dest => dest.Role, opt => opt.MapFrom(src => src.User.Role));

            CreateMap<UserRestaurant, PublicStaffDto>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.User.Id))
                .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.User.Name))
                .ForMember(dest => dest.Surname, opt => opt.MapFrom(src => src.User.Surname))
                .ForMember(dest => dest.Rating, opt => opt.MapFrom(src => src.User.Rating))
                .ForMember(dest => dest.Role, opt => opt.MapFrom(src => src.User.Role.Name))
                .ForMember(dest => dest.MaxActiveTableCount, opt => opt.MapFrom(src => src.MaxActiveTableCount))
                .ForMember(dest => dest.EffectiveMaxActiveTableCount, opt => opt.MapFrom(src =>
                    src.MaxActiveTableCount ?? src.Restaurant.DefaultWaiterTableLimit))
                .ForMember(dest => dest.CanAcceptMoreTables, opt => opt.MapFrom(src =>
                    !(src.MaxActiveTableCount ?? src.Restaurant.DefaultWaiterTableLimit).HasValue))
                .ForMember(dest => dest.FileUrl, opt => opt.Ignore());

            CreateMap<Restaurant, GetByIdRestaurantResponse>()
                .ForMember(dest => dest.Restaurant, opt => opt.MapFrom(src => src))
                .ForMember(dest => dest.Tables, opt => opt.MapFrom(src => src.Tables))
                .ForMember(dest => dest.Categories, opt => opt.MapFrom(src => src.Categories));

            CreateMap<Restaurant, GetAllRestaurantsResponse>()
                .ForMember(dest => dest.RestaurantGroupName, opt => opt.MapFrom(src => src.RestaurantGroup == null ? null : src.RestaurantGroup.Name))
                .ForMember(dest => dest.HasActiveContract, opt => opt.MapFrom(src => src.Contracts.Any(c =>
                    c.StatusId == ((int)ECafe.Domain.Enums.StatusType.Contract * 1000) + (int)ECafe.Domain.Enums.ContractStatus.Active)))
                .ForMember(dest => dest.ImageUrls, opt => opt.Ignore());

            CreateMap<Restaurant, PublicRestaurantListItemDto>()
                .ForMember(dest => dest.RestaurantGroupName, opt => opt.MapFrom(src => src.RestaurantGroup == null ? null : src.RestaurantGroup.Name))
                .ForMember(dest => dest.ImageUrls, opt => opt.Ignore());

            CreateMap<Restaurant, PublicRestaurantDetailDto>()
                .ForMember(dest => dest.RestaurantGroupName, opt => opt.MapFrom(src => src.RestaurantGroup == null ? null : src.RestaurantGroup.Name))
                .ForMember(dest => dest.ImageUrls, opt => opt.Ignore());

            CreateMap<RegisterRestaurantRequest, Restaurant>()
                .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name.Trim()))
                .ForMember(dest => dest.Location, opt => opt.MapFrom(src => src.Location.Trim()))
                .ForMember(dest => dest.Phone, opt => opt.MapFrom(src => src.Phone.Trim()))
                .ForMember(dest => dest.Email, opt => opt.MapFrom(src => src.Email.Trim().ToLowerInvariant()))
                .ForMember(dest => dest.BranchName, opt => opt.MapFrom(src => string.IsNullOrWhiteSpace(src.BranchName) ? null : src.BranchName.Trim()))
                .ForMember(dest => dest.RatingAverage, opt => opt.MapFrom(_ => 0))
                .ForMember(dest => dest.RatingCount, opt => opt.MapFrom(_ => 0))
                .ForMember(dest => dest.IsActive, opt => opt.MapFrom(_ => true))
                .ForMember(dest => dest.RestaurantGroup, opt => opt.Ignore())
                .ForMember(dest => dest.Files, opt => opt.Ignore());
        }
    }
}
