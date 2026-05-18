using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ECafe.Application.Features.Commands.Category
{
    public class CreateCategoryCommand : IRequest<int>
    {
        public int RestaurantId { get; set; }
        public string Name { get; set; } = null!;
        public int SortOrder { get; set; }
    }
}
