using AutoMapper;
using ECafe.Application.DTOs.Category;
using ECafe.Domain.Entities;

namespace ECafe.Application.Mappings
{
    public class CategoryProfile : Profile
    {
        public CategoryProfile()
        {
            CreateMap<CreateCategoryRequest, Category>()
                .ForMember(dest => dest.SortOrder, opt => opt.Ignore());
            CreateMap<UpdateCategoryRequest, Category>()
                .ForMember(dest => dest.RestaurantId, opt => opt.Ignore())
                .ForMember(dest => dest.Slug, opt => opt.Ignore())
                .ForMember(dest => dest.SortOrder, opt => opt.Ignore());
            CreateMap<Category, GetAllCategoryResponse>();
        }
    }
}

