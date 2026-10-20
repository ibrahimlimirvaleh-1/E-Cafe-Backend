using ECafe.Domain.Entities;
using ECafe.Application.DTOs.ContentLocalization;

namespace ECafe.Application.Mappings;

public static class ContentTranslationMapper
{
    /// <summary>Redaktorun restoran sahələrini mövcud tərcümə sətrinə köçürür.</summary>
    public static void Copy(ContentTranslationValue source, RestaurantTranslation target)
    {
        target.Name = source.Name;
        target.Location = source.Location;
        target.BranchName = source.BranchName;
    }

    /// <summary>Redaktorun qrup adını tərcümə sətrinə köçürür.</summary>
    public static void Copy(ContentTranslationValue source, RestaurantGroupTranslation target) => target.Name = source.Name;

    /// <summary>Redaktorun kateqoriya adını tərcümə sətrinə köçürür.</summary>
    public static void Copy(ContentTranslationValue source, CategoryTranslation target) => target.Name = source.Name;

    /// <summary>Redaktorun məhsul mətnlərini tərcümə sətrinə köçürür.</summary>
    public static void Copy(ContentTranslationValue source, ItemTranslation target)
    {
        target.Name = source.Name;
        target.Description = source.Description;
        target.UnavailableReason = source.UnavailableReason;
    }

    /// <summary>Redaktorun masa adını tərcümə sətrinə köçürür.</summary>
    public static void Copy(ContentTranslationValue source, TableTranslation target) => target.Name = source.Name;

    /// <summary>Redaktorun anbar məhsulu adını tərcümə sətrinə köçürür.</summary>
    public static void Copy(ContentTranslationValue source, InventoryItemTranslation target) => target.Name = source.Name;

    /// <summary>Redaktorun ölçü vahidi adını tərcümə sətrinə köçürür.</summary>
    public static void Copy(ContentTranslationValue source, UnitTranslation target) => target.Name = source.Name;

    /// <summary>Restoranın yalnız göstərilən sahələrini köçürür; FK və reviziya dəyişmir.</summary>
    public static void Copy(RestaurantTranslation source, RestaurantTranslation target)
    {
        target.Name = source.Name;
        target.Location = source.Location;
        target.BranchName = source.BranchName;
    }

    /// <summary>Qrupun göstərilən adını köçürür; hüquqi mənbə dəyişmir.</summary>
    public static void Copy(RestaurantGroupTranslation source, RestaurantGroupTranslation target) => target.Name = source.Name;

    /// <summary>Kateqoriyanın tərcümə adını mövcud sətrə köçürür.</summary>
    public static void Copy(CategoryTranslation source, CategoryTranslation target) => target.Name = source.Name;

    /// <summary>Məhsulun yalnız mətnlərini köçürür; qiymət və stok mapping-ə daxil deyil.</summary>
    public static void Copy(ItemTranslation source, ItemTranslation target)
    {
        target.Name = source.Name;
        target.Description = source.Description;
        target.UnavailableReason = source.UnavailableReason;
    }

    /// <summary>Masanın tərcümə adını köçürür; masa nömrəsi dəyişmir.</summary>
    public static void Copy(TableTranslation source, TableTranslation target) => target.Name = source.Name;

    /// <summary>Anbar məhsulunun yalnız tərcümə adını köçürür.</summary>
    public static void Copy(InventoryItemTranslation source, InventoryItemTranslation target) => target.Name = source.Name;

    /// <summary>Ölçü vahidinin göstərilən adını köçürür; texniki kod dəyişmir.</summary>
    public static void Copy(UnitTranslation source, UnitTranslation target) => target.Name = source.Name;
}
