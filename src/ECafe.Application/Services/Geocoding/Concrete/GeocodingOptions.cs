namespace ECafe.Application.Services.Geocoding.Concrete;

public sealed class GeocodingOptions
{
    public string? Provider { get; set; }

    public string? BaseUrl { get; set; }

    public string? UserAgent { get; set; }

    public string? GoogleApiKey { get; set; }

    public string? GoogleBaseUrl { get; set; }

    public string? GoogleLanguage { get; set; }

    public string? GoogleRegion { get; set; }

    public int? TimeoutSeconds { get; set; }

    public int? CacheMinutes { get; set; }

    public string? CountryCodes { get; set; }

    public List<GeocodingKnownPlaceOptions> KnownPlaces { get; set; } = [];
}

public sealed class GeocodingKnownPlaceOptions
{
    public string? DisplayName { get; set; }

    public string? Address { get; set; }

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    public string? PlaceId { get; set; }

    public List<string> Aliases { get; set; } = [];
}
