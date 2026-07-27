using ECafe.Application.DTOs.Notification;
using ECafe.Domain.Enums;
using FluentValidation;
using System.Text.Json;

namespace ECafe.Application.Features.Commands.Notification.Create
{
    public sealed class CreateNotificationRequestValidator : AbstractValidator<CreateNotificationRequest>
    {
        public CreateNotificationRequestValidator()
        {
            RuleFor(x => x.UserId)
                .GreaterThan(0).WithMessage("UserId must be greater than 0.");

            RuleFor(x => x.RestaurantId)
                .GreaterThan(0).WithMessage("RestaurantId must be greater than 0.")
                .When(x => x.RestaurantId.HasValue);

            RuleFor(x => x.Title)
                .NotEmpty().WithMessage("Title is required.")
                .MaximumLength(200).WithMessage("Title must be at most 200 characters.");

            RuleFor(x => x.Message)
                .NotEmpty().WithMessage("Message is required.")
                .MaximumLength(1000).WithMessage("Message must be at most 1000 characters.");

            RuleFor(x => x.TypeId)
                .Must(BeValidNotificationType)
                .WithMessage("TypeId is not a valid notification type.");

            RuleFor(x => x.ChannelId)
                .Must(BeValidNotificationChannel)
                .WithMessage("ChannelId is not a valid notification channel.");

            RuleFor(x => x.PayloadJson)
                .Must(BeValidJson)
                .When(x => !string.IsNullOrWhiteSpace(x.PayloadJson))
                .WithMessage("PayloadJson must be a valid JSON object.");

            RuleFor(x => x.RelatedEntityType)
                .MaximumLength(100).WithMessage("RelatedEntityType must be at most 100 characters.")
                .When(x => x.RelatedEntityType is not null);

            RuleFor(x => x.RelatedEntityId)
                .GreaterThan(0).WithMessage("RelatedEntityId must be greater than 0.")
                .When(x => x.RelatedEntityId.HasValue);

            RuleFor(x => x)
                .Must(HaveRelatedEntityPair)
                .WithMessage("RelatedEntityType and RelatedEntityId must be provided together.");
        }

        private static bool BeValidNotificationType(int typeId)
            => Enum.IsDefined(typeof(NotificationType), typeId);

        private static bool BeValidNotificationChannel(int channelId)
            => Enum.IsDefined(typeof(NotificationChannel), channelId);

        private static bool BeValidJson(string? payloadJson)
        {
            if (string.IsNullOrWhiteSpace(payloadJson))
                return true;

            try
            {
                using var document = JsonDocument.Parse(payloadJson);
                return document.RootElement.ValueKind == JsonValueKind.Object;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        private static bool HaveRelatedEntityPair(CreateNotificationRequest request)
        {
            var hasRelatedEntityType = !string.IsNullOrWhiteSpace(request.RelatedEntityType);
            var hasRelatedEntityId = request.RelatedEntityId.HasValue;

            return hasRelatedEntityType == hasRelatedEntityId;
        }
    }
}
