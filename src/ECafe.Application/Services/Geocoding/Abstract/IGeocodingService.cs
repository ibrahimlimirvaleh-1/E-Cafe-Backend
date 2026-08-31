using ECafe.Application.DTOs.Geocoding;

namespace ECafe.Application.Services.Geocoding.Abstract;

public interface IGeocodingService
{
    Task<GeocodeAddressResponse> GeocodeAddressAsync(string address, CancellationToken cancellationToken = default);
}
