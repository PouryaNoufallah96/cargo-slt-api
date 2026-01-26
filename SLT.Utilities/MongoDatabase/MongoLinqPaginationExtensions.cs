using MongoDB.Driver;
using MongoDB.Driver.Linq;
using Utilities.Utilities;

namespace Utilities.MongoDatabase
{
    public static class MongoLinqPaginationExtensions
    {
        public static async Task<ManualPaginationResult<TQuery>> PaginateLinqAsync<TQuery>(
            this IMongoQueryable<TQuery> query,
            int? pageIndex,
            int? pageSize)
        {
            int index = pageIndex is null or <= 0 ? 1 : pageIndex.Value;
            int size = pageSize is null or <= 0 ? 10 : pageSize.Value;

            var totalCount = await query.CountAsync();
            var pageCount = (int)Math.Ceiling(totalCount / (double)size);

            var data = await query
                .Skip((index - 1) * size)
                .Take(size)
                .ToListAsync();

            return new ManualPaginationResult<TQuery>
            {
                PageCount = pageCount,
                TotalCount = totalCount,
                Data = data
            };
        }
    }
}