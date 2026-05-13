using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ECafe.Application.Features.Commands.Table
{
    public class CreateTableCommand : IRequest<int>
    {
        public int RestaurantId { get; set; }
        public string Name { get; set; }
        public int TableNo { get; set; }
        public int Capacity { get; set; }
    }
}
