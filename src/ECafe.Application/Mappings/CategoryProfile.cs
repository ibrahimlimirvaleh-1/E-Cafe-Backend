using AutoMapper;
using ECafe.Application.DTOs.Category;
using ECafe.Domain.Entities;

namespace ECafe.Application.Mappings
{
    public class CategoryProfile : Profile
    {
        public CategoryProfile()
        {
            CreateMap<CreateCategoryRequest, Category>();
            CreateMap<Category, GetAllCategoryResponse>();
        }
    }
}

