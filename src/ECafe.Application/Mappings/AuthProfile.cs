using AutoMapper;
using ECafe.Application.DTOs.Auth;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using File = ECafe.Domain.Entities.File;

namespace ECafe.Application.Mappings
{
    public class AuthProfile : Profile
    {
        public AuthProfile()
        {
            CreateMap<RegisterRequestDto, User>()
                .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name.Trim()))
                .ForMember(dest => dest.Surname, opt => opt.MapFrom(src => src.Surname.Trim()))
                .ForMember(dest => dest.Email, opt => opt.MapFrom(src => src.Email.Trim().ToLowerInvariant()))
                .ForMember(dest => dest.Phone, opt => opt.MapFrom(src => src.Phone.Trim()))
                .ForMember(dest => dest.Password, opt => opt.MapFrom(src => BCrypt.Net.BCrypt.HashPassword(src.Password)))
                .ForMember(dest => dest.IsActive, opt => opt.MapFrom(_ => true))
                .ForMember(dest => dest.RoleId, opt => opt.MapFrom(_ => (int)RoleCode.Customer))
                .ForMember(dest => dest.FileId, opt => opt.Ignore())
                .ForMember(dest => dest.File, opt => opt.Ignore());

            CreateMap<FileMapData, File>()
                .ForMember(dest => dest.Name, opt => opt.MapFrom(src => Path.GetFileNameWithoutExtension(src.FileName)))
                .ForMember(dest => dest.Extension, opt => opt.MapFrom(src => Path.GetExtension(src.FileName)))
                .ForMember(dest => dest.FileTypeId, opt => opt.MapFrom(src => src.FileTypeId));

            CreateMap<RefreshTokenMapData, UserRefreshToken>();

            CreateMap<AuthTokenMapData, AuthResponseDto>();
        }
    }
}
