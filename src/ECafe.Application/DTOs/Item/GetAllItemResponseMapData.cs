using ECafe.Shared.DTOs;

namespace ECafe.Application.DTOs.Item
{
    public class GetAllItemResponseMapData
    {
        public PaginatedList<ItemDto> Items { get; set; } = null!;
    }
}
