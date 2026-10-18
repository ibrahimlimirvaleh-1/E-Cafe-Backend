using System.Text.Json;
using ECafe.Application.DTOs.Notification;
using ECafe.Application.Repositories.User;
using ECafe.Application.Services.Localization.Abstract;
using ECafe.Domain.Policies;
using Microsoft.EntityFrameworkCore;
using NotificationEntity = ECafe.Domain.Entities.Notification;

namespace ECafe.Application.Services.Localization.Concrete;

public sealed class NotificationTextService(ITranslationService translations, ITextLocalizer text, IUserRepository users) : INotificationTextService
{
    /// <summary>Sistem bildirişinin açar və parametrlərini saxlayıb mətnini əməliyyatı edən şəxsin yox, alıcının dilində e-mail/push outbox-dan əvvəl hazırlayır.</summary>
    public async Task PrepareAsync(NotificationEntity notification, CancellationToken cancellationToken)
    {
        var title = text.ResolveNotificationSource(notification.Title);
        var message = text.ResolveNotificationSource(notification.Message);
        if (title == null && message == null) return;
        var preference = await users.Query(user => user.Id == notification.UserId).Select(user => user.PreferredLanguage).SingleOrDefaultAsync(cancellationToken);
        var language = TranslationPolicy.NormalizeLanguage(preference);
        var snapshot = await translations.GetPublishedAsync(cancellationToken);
        var dictionary = snapshot.ForLanguage(language).Messages;
        notification.TitleKey = title?.Key;
        notification.MessageKey = message?.Key;
        notification.TranslationArgumentsJson = JsonSerializer.Serialize(new Arguments(title?.Arguments, message?.Arguments));
        notification.Language = language;
        notification.TranslationVersion = snapshot.Version;
        if (title != null) notification.Title = Limit(TextLocalizer.Render(dictionary, title.Key, notification.Title, title.Arguments), 200);
        if (message != null) notification.Message = Limit(TextLocalizer.Render(dictionary, message.Key, notification.Message, message.Arguments), 1000);
    }

    /// <summary>Açarlı bildirişi oxuyanın cari dilində göstərir; əvvəlki açarsız bildirişlərin saxlanmış mətninə toxunmur.</summary>
    public void LocalizeForReader(NotificationEntity notification, NotificationResponse response)
    {
        if (notification.TranslationArgumentsJson == null) return;
        var arguments = JsonSerializer.Deserialize<Arguments>(notification.TranslationArgumentsJson);
        if (notification.TitleKey != null) response.Title = Limit(text.Get(notification.TitleKey, notification.Title, arguments?.Title), 200);
        if (notification.MessageKey != null) response.Message = Limit(text.Get(notification.MessageKey, notification.Message, arguments?.Message), 1000);
    }
    /// <summary>Uzun parametr və admin mətni bildirişin DB məhdudiyyətini pozaraq əsas biznes transaction-ını dayandırmasın deyə yalnız görünən mətnin sonunu qısaldır.</summary>
    private static string Limit(string value, int length) => value.Length <= length ? value : value[..(length - 3)] + "...";
    public sealed record Arguments(IReadOnlyDictionary<string, object?>? Title, IReadOnlyDictionary<string, object?>? Message);
}
