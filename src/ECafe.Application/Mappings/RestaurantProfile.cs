using AutoMapper;
using ECafe.Application.DTOs.Restaurant;
using ECafe.Domain.Entities;

namespace ECafe.Application.Mappings
{
    public class RestaurantProfile : Profile
    {
        public RestaurantProfile()
        {
            CreateMap<Restaurant, RestaurantDetailDto>();

            CreateMap<Table, TableDto>();

            CreateMap<Category, CategoryDto>();

            CreateMap<Item, ItemDto>();

            CreateMap<Role, RoleDto>();

            CreateMap<User, UserDto>()
                .ForMember(
                    dest => dest.Role,
                    opt => opt.MapFrom(src => src.Role)
                );

            CreateMap<UserRestaurant, UserDto>()
                .ForMember(
                    dest => dest.Id,
                    opt => opt.MapFrom(src => src.User.Id)
                )
                .ForMember(
                    dest => dest.Name,
                    opt => opt.MapFrom(src => src.User.Name)
                )
                .ForMember(
                    dest => dest.Surname,
                    opt => opt.MapFrom(src => src.User.Surname)
                )
                .ForMember(
                    dest => dest.Email,
                    opt => opt.MapFrom(src => src.User.Email)
                )
                .ForMember(
                    dest => dest.Phone,
                    opt => opt.MapFrom(src => src.User.Phone)
                )
                .ForMember(
                    dest => dest.Role,
                    opt => opt.MapFrom(src => src.User.Role)
                );

            CreateMap<Restaurant, GetByIdRestaurantResponse>()
                .ForMember(
                    dest => dest.Restaurant,
                    opt => opt.MapFrom(src => src)
                )
                .ForMember(
                    dest => dest.Tables,
                    opt => opt.MapFrom(src => src.Tables)
                )
                .ForMember(
                    dest => dest.Categories,
                    opt => opt.MapFrom(src => src.Categories)
                )
                .ForMember(
                    dest => dest.Users,
                    opt => opt.MapFrom(src => src.UserRestaurants)
                );
        }
    }
}
