using AutoMapper;
using ECafe.Application.DTOs.InventoryItem;
using ECafe.Application.DTOs.Recipe;
using ECafe.Domain.Entities;

namespace ECafe.Application.Mappings
{
    public class RecipeProfile : Profile
    {
        public RecipeProfile()
        {
            CreateMap<CreateRecipeRequest, Recipe>()
                .ForMember(dest => dest.RestaurantId, opt => opt.Ignore())
                .ForMember(dest => dest.ItemId, opt => opt.Ignore())
                .ForMember(dest => dest.IsActive, opt => opt.MapFrom(_ => true))
                .ForMember(dest => dest.Item, opt => opt.Ignore())
                .ForMember(dest => dest.InventoryItem, opt => opt.Ignore())
                .ForMember(dest => dest.Unit, opt => opt.Ignore());

            CreateMap<UpdateRecipeRequest, Recipe>()
                .ForMember(dest => dest.RestaurantId, opt => opt.Ignore())
                .ForMember(dest => dest.ItemId, opt => opt.Ignore())
                .ForMember(dest => dest.Item, opt => opt.Ignore())
                .ForMember(dest => dest.InventoryItem, opt => opt.Ignore())
                .ForMember(dest => dest.Unit, opt => opt.Ignore());

            CreateMap<Recipe, RecipeDto>()
                .ForMember(dest => dest.ItemName, opt => opt.MapFrom(src => src.Item.Name))
                .ForMember(dest => dest.InventoryItemName, opt => opt.MapFrom(src => src.InventoryItem.Name))
                .ForMember(dest => dest.UnitName, opt => opt.MapFrom(src => src.Unit.Name))
                .ForMember(dest => dest.UnitCode, opt => opt.MapFrom(src => src.Unit.Code));

            CreateMap<Recipe, DeleteOrDeactivateResponse>();
        }
    }
}
