namespace ECafe.Application.Services.Geocoding.Concrete;

public sealed class GeocodingOptions
{
    public string? Provider { get; set; }

    public string? BaseUrl { get; set; }

    public string? ApiKey { get; set; }

    public string? Language { get; set; }

    public string? Bias { get; set; }

    public int? TimeoutSeconds { get; set; }

    public int? CacheMinutes { get; set; }

    public string? CountryCodes { get; set; }
}
