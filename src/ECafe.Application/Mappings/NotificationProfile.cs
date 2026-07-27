using AutoMapper;
using ECafe.Application.DTOs.Notification;
using ECafe.Domain.Enums;

namespace ECafe.Application.Mappings
{
    public class NotificationProfile : Profile
    {
        public NotificationProfile()
        {
            CreateMap<CreateNotificationRequest, Domain.Entities.Notification>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.StatusId, opt => opt.MapFrom(_ => (int)NotificationStatus.Unread))
                .ForMember(dest => dest.IsRead, opt => opt.MapFrom(_ => false))
                .ForMember(dest => dest.ReadAt, opt => opt.Ignore())
                .ForMember(dest => dest.User, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedBy, opt => opt.Ignore())
                .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.UpdatedBy, opt => opt.Ignore())
                .ForMember(dest => dest.IsDeleted, opt => opt.Ignore())
                .ForMember(dest => dest.DeletedAt, opt => opt.Ignore())
                .ForMember(dest => dest.DeletedBy, opt => opt.Ignore());

            CreateMap<Domain.Entities.Notification, NotificationResponse>()
                .ForMember(dest => dest.TypeName, opt => opt.MapFrom(src => GetNotificationTypeName(src.TypeId)));
        }

        private static string GetNotificationTypeName(int typeId)
            => Enum.GetName(typeof(NotificationType), typeId) ?? typeId.ToString();
    }
}
