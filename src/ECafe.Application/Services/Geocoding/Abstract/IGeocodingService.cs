using ECafe.Application.DTOs.Geocoding;

namespace ECafe.Application.Services.Geocoding.Abstract;

public interface IGeocodingService
{
    Task<IReadOnlyList<GeocodeAddressResponse>> SearchAddressesAsync(string address, int limit, CancellationToken cancellationToken = default);
}
