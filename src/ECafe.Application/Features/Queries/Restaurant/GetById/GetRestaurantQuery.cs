using ECafe.Application.DTOs.Restaurant;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ECafe.Application.Features.Queries.Restaurant.GetById
{
    public class GetRestaurantQuery : IRequest<GetAllRestaurantsResponse>
    {
    }
}
