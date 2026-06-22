using AutoMapper;
using ECafe.Application.DTOs.Item;

namespace ECafe.Application.Mappings
{
    public class ItemProfile : Profile
    {
        public ItemProfile()
        {
            CreateMap<CreateItemRequest, Domain.Entities.Item>()
                .ForMember(dest => dest.CategoryId, opt => opt.MapFrom(src => src.CategoryId))
                .ForMember(dest => dest.RestaurantId, opt => opt.MapFrom(src => src.RestaurantId))
                .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name.Trim()))
                .ForMember(dest => dest.Description, opt => opt.MapFrom(src => src.Description == null ? null : src.Description.Trim()))
                .ForMember(dest => dest.BasePrice, opt => opt.MapFrom(src => src.BasePrice))
                .ForMember(dest => dest.UnavailableReason, opt => opt.MapFrom(src => src.UnavailableReason == null ? null : src.UnavailableReason.Trim()))
                .ForMember(dest => dest.SalesCount, opt => opt.MapFrom(src => src.SalesCount))
                .ForMember(dest => dest.IsActive, opt => opt.MapFrom(_ => true))
                .ForMember(dest => dest.IsAvailable, opt => opt.MapFrom(src => string.IsNullOrWhiteSpace(src.UnavailableReason)))
                .ForMember(dest => dest.File, opt => opt.Ignore());
        }
    }
}
