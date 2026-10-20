namespace ECafe.Application.DTOs.Table
{
    public class CreateTableRequest
    {
        public List<TableTranslationRequest>? Translations { get; set; }
        public int TableNo { get; set; }

        public string? Name { get; set; }

        public int Capacity { get; set; }
    }
}
