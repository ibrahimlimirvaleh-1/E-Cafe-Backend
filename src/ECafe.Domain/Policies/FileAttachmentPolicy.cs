using System.Globalization;
using ECafe.Domain.Enums;

namespace ECafe.Domain.Policies;

public static class FileAttachmentPolicy
{
    /// <summary>İctimai şəkil təyinatına yalnız uyğun şəkil tipini və yükləyicinin öz faylını qəbul edir; admin format yoxlamasını keçə bilmir.</summary>
    public static bool CanAttachImage(Entities.File file, int actorId, bool isPlatformAdmin)
        => actorId > 0
           && (isPlatformAdmin || string.Equals(file.CreatedBy, actorId.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal))
           && (file.FileTypeId is (int)FileTypeCode.TemporaryUpload or (int)FileTypeCode.UserProfileImage
               or (int)FileTypeCode.RestaurantImage or (int)FileTypeCode.MenuItemImage)
           && (file.Extension?.ToLowerInvariant() is ".jpg" or ".jpeg" or ".png" or ".webp" or ".avif");
}
                                                