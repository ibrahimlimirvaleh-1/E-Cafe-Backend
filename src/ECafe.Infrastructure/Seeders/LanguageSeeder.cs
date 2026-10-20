using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Domain.Policies;
using ECafe.Shared.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ECafe.Infrastructure.Seeders;

public static class LanguageSeeder
{
    /// <summary>Enum-dakı sabit dil ID-lərini və kodlarını mövcud baza dəyərlərini dəyişmədən model seed-inə əlavə edir.</summary>
    public static void Seed(ModelBuilder modelBuilder)
        => modelBuilder.Entity<Language>().HasData(Enum.GetValues<LanguageCode>().Select(language => new Language
        {
            Id = (int)language,
            Code = TranslationPolicy.Code(language),
            Name = language.GetName(),
            IsActive = true,
            IsDefault = language == TranslationPolicy.DefaultLanguageCode
        }));
}
