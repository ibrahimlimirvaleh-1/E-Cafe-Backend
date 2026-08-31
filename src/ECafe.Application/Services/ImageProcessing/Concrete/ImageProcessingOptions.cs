namespace ECafe.Application.Services.ImageProcessing.Concrete;

public sealed class ImageProcessingOptions
{
    public bool Enabled { get; set; } = true;

    public int MaxWidth { get; set; } = 1440;

    public int MaxHeight { get; set; } = 1440;

    public int WebpQuality { get; set; } = 82;

    public int AvifQuality { get; set; } = 74;

    public string OutputFormat { get; set; } = "webp";

    public string OptimizedFileTypeCodes { get; set; } = "RestaurantImage,MenuItemImage,UserProfileImage,TemporaryUpload";
}
