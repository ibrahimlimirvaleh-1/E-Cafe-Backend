using ECafe.Application.DTOs.Geocoding;
using ECafe.Application.Services.Geocoding.Abstract;
using MediatR;

namespace ECafe.Application.Features.Queries.Geocoding;

public sealed class GeocodeAddressQuery : IRequest<IReadOnlyList<GeocodeAddressResponse>>
{
    public string Address { get; set; } = null!;

    public int Limit { get; set; } = 5;

    public sealed class Handler : IRequestHandler<GeocodeAddressQuery, IReadOnlyList<GeocodeAddressResponse>>
    {
        private readonly IGeocodingService _geocodingService;

        public Handler(IGeocodingService geocodingService)
        {
            _geocodingService = geocodingService;
        }

        public Task<IReadOnlyList<GeocodeAddressResponse>> Handle(GeocodeAddressQuery request, CancellationToken cancellationToken)
            => _geocodingService.SearchAddressesAsync(request.Address, request.Limit, cancellationToken);
    }
}
