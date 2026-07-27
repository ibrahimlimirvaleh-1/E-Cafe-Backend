using AutoMapper;
using ECafe.Application.Services.InventoryItem.Abstract;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace ECafe.Application.Services.InventoryItem.Concrete
{
    public class InventoryItemManager : BaseManager, IInventoryItemService
    {
        public InventoryItemManager(IHttpContextAccessor httpContextAccessor, 
            IMapper mapper, IConfiguration configuration) 
            : base(httpContextAccessor, mapper, configuration)
        {
        }
    }
}
