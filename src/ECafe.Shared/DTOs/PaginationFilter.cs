namespace ECafe.Shared.DTOs
{
    public sealed class PaginationFilter
    {
        public PaginationFilter()
        {
        }
        public PaginationFilter(int pageNumber, int pageSize)
        {
            PageNumber = pageNumber;
            PageSize = pageSize;
        }

        public int PageNumber { get; set; }
        public int PageSize { get; set; }
    }
}



