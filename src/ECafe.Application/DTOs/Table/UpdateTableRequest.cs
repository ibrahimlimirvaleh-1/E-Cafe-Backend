namespace ECafe.Application.DTOs.Table
{
    public class UpdateTableRequest
    {
        public List<TableTranslationRequest>? Translations { get; set; }
        public int TableNo { get; set; }

        public string? Name { get; set; }

        public int Capacity { get; set; }

        public bool IsActive { get; set; }
    }
}
