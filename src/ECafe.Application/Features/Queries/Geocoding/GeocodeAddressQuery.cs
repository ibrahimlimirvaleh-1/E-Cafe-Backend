using ECafe.Application.DTOs.Geocoding;
using ECafe.Application.Services.Geocoding.Abstract;
using MediatR;

namespace ECafe.Application.Features.Queries.Geocoding;

public sealed class GeocodeAddressQuery : IRequest<GeocodeAddressResponse>
{
    public string Address { get; set; } = null!;

    public sealed class Handler : IRequestHandler<GeocodeAddressQuery, GeocodeAddressResponse>
    {
        private readonly IGeocodingService _geocodingService;

        public Handler(IGeocodingService geocodingService)
        {
            _geocodingService = geocodingService;
        }

        public Task<GeocodeAddressResponse> Handle(GeocodeAddressQuery request, CancellationToken cancellationToken)
            => _geocodingService.GeocodeAddressAsync(request.Address, cancellationToken);
    }
}
