using ECafe.Application.DTOs.Item;

namespace ECafe.Api.Requests.Item
{
    public sealed class UpdateItemFormRequest
    {
        public List<ItemTranslationRequest>? Translations { get; set; }

        public bool ClearTranslations { get; set; }

        public int CategoryId { get; set; }

        public int StatusId { get; set; } = 5001;

        public string Name { get; set; } = null!;

        public string? Description { get; set; }

        public decimal BasePrice { get; set; }

        public string? UnavailableReason { get; set; }

        public int SalesCount { get; set; }

        public int? FileId { get; set; }
    }
}
