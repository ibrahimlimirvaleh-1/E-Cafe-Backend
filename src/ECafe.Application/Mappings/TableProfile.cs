using AutoMapper;
using ECafe.Application.DTOs.Table;
using ECafe.Domain.Entities;

namespace ECafe.Application.Mappings
{
    public class TableProfile : Profile
    {
        public TableProfile()
        {
            CreateMap<CreateTableRequest, Table>()
                .ForMember(dest => dest.IsActive, opt => opt.MapFrom(_ => true))
                .ForMember(dest => dest.IsEmpty, opt => opt.MapFrom(_ => true));

            CreateMap<UpdateTableRequest, Table>()
                .ForMember(dest => dest.RestaurantId, opt => opt.Ignore())
                .ForMember(dest => dest.IsEmpty, opt => opt.Ignore());
        }
    }
}
