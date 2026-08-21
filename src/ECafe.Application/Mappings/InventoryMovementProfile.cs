using AutoMapper;
using ECafe.Application.DTOs.InventoryMovement;

namespace ECafe.Application.Mappings
{
    public class InventoryMovementProfile : Profile
    {

        public InventoryMovementProfile()
        {
            CreateMap<CreateInventoryMovementRequest, Domain.Entities.InventoryMovement>()
                .ForMember(dest => dest.QuantityChange, opt => opt.Ignore())
                .ForMember(dest => dest.UnitId, opt => opt.MapFrom(src => src.UnitId))
                .ForMember(dest => dest.MovementTypeId, opt => opt.MapFrom(src => src.MovementTypeId))
                .ForMember(dest => dest.Reason, opt => opt.MapFrom(src => string.IsNullOrWhiteSpace(src.Reason) ? null : src.Reason.Trim()))
                .ForMember(dest => dest.RestaurantId, opt => opt.Ignore())
                .ForMember(dest => dest.InventoryItemId, opt => opt.Ignore())
                .ForMember(dest => dest.RelatedOrderId, opt => opt.Ignore())
                .ForMember(dest => dest.InventoryItem, opt => opt.Ignore())
                .ForMember(dest => dest.Unit, opt => opt.Ignore())
                .ForMember(dest => dest.MovementType, opt => opt.Ignore());

            CreateMap<Domain.Entities.InventoryMovement, InventoryMovementResponse>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
                .ForMember(dest => dest.RestaurantId, opt => opt.MapFrom(src => src.RestaurantId))
                .ForMember(dest => dest.InventoryItemId, opt => opt.MapFrom(src => src.InventoryItemId))
                .ForMember(dest => dest.QuantityChange, opt => opt.MapFrom(src => src.QuantityChange))
                .ForMember(dest => dest.UnitId, opt => opt.MapFrom(src => src.UnitId))
                .ForMember(dest => dest.UnitName, opt => opt.MapFrom(src => src.Unit.Name))
                .ForMember(dest => dest.MovementTypeId, opt => opt.MapFrom(src => src.MovementTypeId))
                .ForMember(dest => dest.MovementType, opt => opt.MapFrom(src => src.MovementType.Name))
                .ForMember(dest => dest.MovementTypeCode, opt => opt.MapFrom(src => src.MovementType.Code))
                .ForMember(dest => dest.Reason, opt => opt.MapFrom(src => src.Reason))
                .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => src.CreatedAt));


            CreateMap<Domain.Entities.InventoryMovement, InventoryMovementHistoryResponse>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
                .ForMember(dest => dest.RestaurantId, opt => opt.MapFrom(src => src.RestaurantId))
                .ForMember(dest => dest.InventoryItemId, opt => opt.MapFrom(src => src.InventoryItemId))
                .ForMember(dest => dest.QuantityChange, opt => opt.MapFrom(src => src.QuantityChange))
                .ForMember(dest => dest.UnitId, opt => opt.MapFrom(src => src.UnitId))
                .ForMember(dest => dest.UnitName, opt => opt.MapFrom(src => src.Unit.Name))
                .ForMember(dest => dest.MovementTypeId, opt => opt.MapFrom(src => src.MovementTypeId))
                .ForMember(dest => dest.MovementType, opt => opt.MapFrom(src => src.MovementType.Name))
                .ForMember(dest => dest.Reason, opt => opt.MapFrom(src => src.Reason))
                .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => src.CreatedAt));

        }   
    }
}
