using ECafe.Domain.Constants;
using ECafe.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ECafe.Infrastructure.Seeders;

public static class TranslationCatalogSeeder
{
    /// <summary>Sistem kataloqunun ilkin boş göstəricisini yaradır; işləyən bazanın redaktə/yayım mətnlərini yenidən yazmır.</summary>
    public static void Seed(ModelBuilder modelBuilder)
        => modelBuilder.Entity<TranslationCatalog>().HasData(new TranslationCatalog { Id = LocalizationDefaults.CatalogId, Revision = 0 });
}
