using ECafe.Shared.DTOs;

namespace ECafe.Application.Common.Pagination
{
    public static class PaginationFilterNormalizer
    {
        private const int DefaultMaxPageSize = 100;

        public static PaginationFilter Normalize(
            PaginationFilter? filter,
            int defaultPageSize = 5,
            int maxPageSize = DefaultMaxPageSize)
        {
            filter ??= new PaginationFilter();

            filter.PageNumber = NormalizePageNumber(filter.PageNumber);
            filter.PageSize = NormalizePageSize(filter.PageSize, defaultPageSize, maxPageSize);

            return filter;
        }

        public static int NormalizePageNumber(int pageNumber)
            => pageNumber <= 0 ? 1 : pageNumber;

        public static int NormalizePageSize(
            int pageSize,
            int defaultPageSize,
            int maxPageSize = DefaultMaxPageSize)
        {
            if (pageSize <= 0)
                return defaultPageSize;

            return pageSize > maxPageSize ? maxPageSize : pageSize;
        }
    }
}
