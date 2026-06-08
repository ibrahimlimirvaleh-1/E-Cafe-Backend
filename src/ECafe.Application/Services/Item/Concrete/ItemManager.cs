using AutoMapper;
using ECafe.Application.Services.Item.Abstract;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace ECafe.Application.Services.Item.Concrete
{
    public class ItemManager : BaseManager, IItemService
    {
        public ItemManager(IHttpContextAccessor httpContextAccessor, 
                           IMapper mapper, IConfiguration configuration) 
                           : base(httpContextAccessor, mapper, configuration)
        {
        }
    }
}
