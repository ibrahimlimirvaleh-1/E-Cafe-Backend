using AutoMapper;
using Microsoft.EntityFrameworkCore;

namespace ECafe.Shared.DTOs

{
    public sealed class PaginatedMappedList<T, TY>
    {
        public List<T> Items { get; }
        public int PageIndex { get; }
        public int TotalPages { get; }
        public int TotalCount { get; }

        public PaginatedMappedList(List<T> items, int count, int pageIndex, int pageSize)
        {
            PageIndex = pageIndex;
            TotalPages = (int)Math.Ceiling(count / (double)pageSize);
            TotalCount = count;
            Items = items;
        }

        public bool HasPreviousPage => PageIndex > 1;

        public bool HasNextPage => PageIndex < TotalPages;

        public static async Task<PaginatedMappedList<T, TY>> CreateAsync(IQueryable<TY> source, IMapper mapper, int pageIndex, int pageSize)
        {
            var count = await source.CountAsync();
            var items = await source.Skip((pageIndex - 1) * pageSize).Take(pageSize).ToListAsync();
            var dtos = mapper.Map<List<TY>, List<T>>(items);

            return new PaginatedMappedList<T, TY>(dtos, count, pageIndex, pageSize);
        }
    }
}
