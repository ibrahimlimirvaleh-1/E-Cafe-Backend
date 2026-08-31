namespace ECafe.Application.DTOs.Geocoding;

public sealed class GeocodeAddressResponse
{
    public string DisplayName { get; set; } = null!;

    public double Latitude { get; set; }

    public double Longitude { get; set; }

    public string? PlaceId { get; set; }
}
