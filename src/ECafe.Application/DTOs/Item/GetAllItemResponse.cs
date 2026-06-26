using ECafe.Shared.DTOs;

namespace ECafe.Application.DTOs.Item
{
    public class GetAllItemResponse
    {
        public PaginatedList<ItemDto> Items { get; set; } = null!;
    }
}
