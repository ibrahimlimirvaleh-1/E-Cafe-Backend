using AutoMapper;
using ECafe.Application.DTOs.InventoryItem;

namespace ECafe.Application.Mappings
{
    public class InventoryItemProfile : Profile
    {
        public InventoryItemProfile()
        {
            CreateMap<CreateInventoryItemRequest, Domain.Entities.InventoryItem>()
                .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name.Trim()))
                .ForMember(dest => dest.IsActive, opt => opt.MapFrom(_ => true))
                .ForMember(dest => dest.RestaurantId, opt => opt.Ignore())
                .ForMember(dest => dest.Restaurant, opt => opt.Ignore())
                .ForMember(dest => dest.Unit, opt => opt.Ignore())
                .ForMember(dest => dest.Recipes, opt => opt.Ignore())
                .ForMember(dest => dest.Movements, opt => opt.Ignore());

            CreateMap<Domain.Entities.InventoryItem, InventoryItemDto>()
                .ForMember(dest => dest.UnitName, opt => opt.MapFrom(src => src.Unit.Name))
                .ForMember(dest => dest.UnitCode, opt => opt.MapFrom(src => src.Unit.Code))
                .ForMember(dest => dest.IsLowStock, opt => opt.MapFrom(src => src.QuantityOnHand <= src.LowStockThreshold));

            CreateMap<Domain.Entities.InventoryItem, DeleteOrDeactivateResponse>();

            CreateMap<UpdateInventoryItemRequest, Domain.Entities.InventoryItem>()
                .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name.Trim()))
                .ForMember(dest => dest.IsActive, opt => opt.MapFrom(src => src.IsActive))
                .ForMember(dest => dest.UnitId, opt => opt.MapFrom(src => src.UnitId))
                .ForMember(dest => dest.LowStockThreshold, opt => opt.MapFrom(src => src.LowStockThreshold))
                .ForMember(dest => dest.RestaurantId, opt => opt.Ignore())
                .ForMember(dest => dest.QuantityOnHand, opt => opt.Ignore())
                .ForMember(dest => dest.LastLowStockNotifiedAt, opt => opt.Ignore())
                .ForMember(dest => dest.Restaurant, opt => opt.Ignore())
                .ForMember(dest => dest.Unit, opt => opt.Ignore())
                .ForMember(dest => dest.Recipes, opt => opt.Ignore())
                .ForMember(dest => dest.Movements, opt => opt.Ignore());


        }
    }
}
