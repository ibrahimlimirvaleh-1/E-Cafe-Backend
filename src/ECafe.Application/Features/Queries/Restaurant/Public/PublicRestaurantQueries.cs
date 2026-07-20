using ECafe.Application.DTOs.Restaurant.Public;
using ECafe.Application.Services.Restaurant.Abstract;
using MediatR;

namespace ECafe.Application.Features.Queries.Restaurant.Public
{
    public record GetPublicRestaurantsQuery : IRequest<List<PublicRestaurantListItemDto>>;

    public class GetPublicRestaurantsQueryHandler
        : IRequestHandler<GetPublicRestaurantsQuery, List<PublicRestaurantListItemDto>>
    {
        private readonly IRestaurantService _restaurantService;

        public GetPublicRestaurantsQueryHandler(IRestaurantService restaurantService)
        {
            _restaurantService = restaurantService;
        }

        public Task<List<PublicRestaurantListItemDto>> Handle(
            GetPublicRestaurantsQuery request,
            CancellationToken cancellationToken)
            => _restaurantService.GetPublicRestaurantsAsync();
    }

    public record GetPublicRestaurantProfileQuery(int RestaurantId) : IRequest<PublicRestaurantProfileDto>;

    public class GetPublicRestaurantProfileQueryHandler
        : IRequestHandler<GetPublicRestaurantProfileQuery, PublicRestaurantProfileDto>
    {
        private readonly IRestaurantService _restaurantService;

        public GetPublicRestaurantProfileQueryHandler(IRestaurantService restaurantService)
        {
            _restaurantService = restaurantService;
        }

        public Task<PublicRestaurantProfileDto> Handle(
            GetPublicRestaurantProfileQuery request,
            CancellationToken cancellationToken)
            => _restaurantService.GetPublicRestaurantProfileAsync(request.RestaurantId);
    }

    public record GetPublicRestaurantMenuQuery(int RestaurantId) : IRequest<List<PublicMenuCategoryDto>>;

    public class GetPublicRestaurantMenuQueryHandler
        : IRequestHandler<GetPublicRestaurantMenuQuery, List<PublicMenuCategoryDto>>
    {
        private readonly IRestaurantService _restaurantService;

        public GetPublicRestaurantMenuQueryHandler(IRestaurantService restaurantService)
        {
            _restaurantService = restaurantService;
        }

        public Task<List<PublicMenuCategoryDto>> Handle(
            GetPublicRestaurantMenuQuery request,
            CancellationToken cancellationToken)
            => _restaurantService.GetPublicRestaurantMenuAsync(request.RestaurantId);
    }

    public record GetPublicRestaurantStaffQuery(int RestaurantId) : IRequest<List<PublicStaffDto>>;

    public class GetPublicRestaurantStaffQueryHandler
        : IRequestHandler<GetPublicRestaurantStaffQuery, List<PublicStaffDto>>
    {
        private readonly IRestaurantService _restaurantService;

        public GetPublicRestaurantStaffQueryHandler(IRestaurantService restaurantService)
        {
            _restaurantService = restaurantService;
        }

        public Task<List<PublicStaffDto>> Handle(
            GetPublicRestaurantStaffQuery request,
            CancellationToken cancellationToken)
            => _restaurantService.GetPublicRestaurantStaffAsync(request.RestaurantId);
    }

    public record GetPublicRestaurantTablesQuery(int RestaurantId) : IRequest<List<PublicTableDto>>;

    public class GetPublicRestaurantTablesQueryHandler
        : IRequestHandler<GetPublicRestaurantTablesQuery, List<PublicTableDto>>
    {
        private readonly IRestaurantService _restaurantService;

        public GetPublicRestaurantTablesQueryHandler(IRestaurantService restaurantService)
        {
            _restaurantService = restaurantService;
        }

        public Task<List<PublicTableDto>> Handle(
            GetPublicRestaurantTablesQuery request,
            CancellationToken cancellationToken)
            => _restaurantService.GetPublicRestaurantTablesAsync(request.RestaurantId);
    }
}
