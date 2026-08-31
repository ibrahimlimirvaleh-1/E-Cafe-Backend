using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Shared.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ECafe.Infrastructure.Seeders
{
    public static class FileTypeSeeder
    {
        public static void Seed(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<FileType>().HasData(
                FileType(FileTypeCode.RestaurantImage, "restaurant_image", true, ".jpg,.jpeg,.png,.webp,.avif", "image/jpeg,image/png,image/webp,image/avif", 10),
                FileType(FileTypeCode.MenuItemImage, "menu_item_image", true, ".jpg,.jpeg,.png,.webp,.avif", "image/jpeg,image/png,image/webp,image/avif", 10),
                FileType(FileTypeCode.UserProfileImage, "user_profile_image", true, ".jpg,.jpeg,.png,.webp,.avif", "image/jpeg,image/png,image/webp,image/avif", 5),
                FileType(FileTypeCode.ContractDocument, "contract_document", false, ".pdf,.doc,.docx", "application/pdf,application/msword,application/vnd.openxmlformats-officedocument.wordprocessingml.document", 10),
                FileType(FileTypeCode.InvoiceDocument, "invoice_document", false, ".pdf", "application/pdf", 10),
                FileType(FileTypeCode.PaymentReceipt, "payment_receipt", false, ".pdf,.jpg,.jpeg,.png,.webp,.avif", "application/pdf,image/jpeg,image/png,image/webp,image/avif", 10),
                FileType(FileTypeCode.AdminDocument, "admin_document", false, ".pdf,.doc,.docx", "application/pdf,application/msword,application/vnd.openxmlformats-officedocument.wordprocessingml.document", 10),
                FileType(FileTypeCode.TemporaryUpload, "temporary_upload", false, ".jpg,.jpeg,.png,.webp,.avif,.pdf,.doc,.docx", "image/jpeg,image/png,image/webp,image/avif,application/pdf,application/msword,application/vnd.openxmlformats-officedocument.wordprocessingml.document", 10));
        }

        private static FileType FileType(
            FileTypeCode fileType,
            string code,
            bool isPublic,
            string allowedExtensions,
            string allowedMimeTypes,
            int maxSizeMb)
            => new()
            {
                Id = (int)fileType,
                Name = fileType.GetName(),
                Code = code,
                IsPublic = isPublic,
                AllowedExtensions = allowedExtensions,
                AllowedMimeTypes = allowedMimeTypes,
                MaxSizeMb = maxSizeMb,
                CreatedAt = DateTime.MinValue,
                CreatedBy = string.Empty,
                IsDeleted = false
            };
    }
}
