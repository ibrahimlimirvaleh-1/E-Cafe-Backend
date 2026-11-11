namespace ECafe.Application.Services.ImageProcessing.Concrete;

public sealed class ImageProcessingOptions
{
    public bool Enabled { get; set; } = true;

    public int MaxWidth { get; set; } = 1440;

    public int MaxHeight { get; set; } = 1440;

    public int MaxInputDimension { get; set; } = 12000;

    public long MaxInputPixels { get; set; } = 24_000_000;

    public int MaxInputFrames { get; set; } = 16;

    public int MaxConcurrentOperations { get; set; } = 2;

    public int QueueTimeoutSeconds { get; set; } = 5;

    public int WebpQuality { get; set; } = 82;

    public int AvifQuality { get; set; } = 74;

    public string OutputFormat { get; set; } = "webp";

    public string OptimizedFileTypeCodes { get; set; } = "RestaurantImage,MenuItemImage,UserProfileImage,TemporaryUpload";
}
