using ECafe.Application.Services.Restaurant.Abstract;
using ECafe.Domain.Enums;
using MediatR;
using System.Collections;

namespace ECafe.Application.Features.Queries.Restaurant.GetAll
{
    public record GetRestaurantStaffQuery(int RestaurantId, int RoleId) : IRequest<IList>;

    public class GetRestaurantStaffHandler : IRequestHandler<GetRestaurantStaffQuery, IList>
    {
        private readonly IRestaurantService _staffService;

        public GetRestaurantStaffHandler(IRestaurantService staffService)
        {
            _staffService = staffService;
        }

        public async Task<IList> Handle(GetRestaurantStaffQuery request, CancellationToken cancellationToken)
        {
            if (request.RoleId != (int)RoleCode.Customer)
                return await _staffService.GetRestaurantStaffAsync(request.RestaurantId);

            return await _staffService.GetRestaurantPublicStaffAsync(request.RestaurantId);
        }
    }

}
