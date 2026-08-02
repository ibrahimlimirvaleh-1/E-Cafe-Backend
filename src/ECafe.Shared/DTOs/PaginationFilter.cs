namespace ECafe.Shared.DTOs
{
    public sealed class PaginationFilter
    {
        public PaginationFilter()
        {
            PageNumber = 1;
            PageSize = 20;
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



