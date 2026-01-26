using MongoDB.Driver;
using MongoDB.Driver.Linq;
using Utilities.MongoDatabase.Filter;

namespace Utilities.MongoDatabase.Extensions
{
    public static class IMongoQueryableExtensions
    {
        public static IMongoQueryable<T> AsMongoQueryable<T>(this IQueryable<T> queryable)
        {
            return (IMongoQueryable<T>)queryable;
        }

        public static IMongoQueryable<T> Apply<T>(this IMongoQueryable<T> query, IList<IList<MonjoCondition>> monjoConditions, string collectionName)
        {
            return monjoConditions.Apply(query, collectionName);
        }

        public static IMongoQueryable<T> Apply<T>(this IMongoQueryable<T> query, IList<MonjoOrder> monjoOrders, string collectionName)
        {
            return monjoOrders.Apply(query, collectionName);
        }

        public static IMongoQueryable<T> Apply<T>(this IMongoQueryable<T> query, IList<IList<MonjoCondition>> monjoConditions)
        {
            return monjoConditions.Apply(query, typeof(T).Name);
        }

        public static IMongoQueryable<T> Apply<T>(this IMongoQueryable<T> query, IList<MonjoOrder> monjoOrders)
        {
            return monjoOrders.Apply(query, typeof(T).Name);
        }

        public static IMongoQueryable<T> Apply<T>(this IMongoQueryable<T> query, MonjoPage monjoPage)
        {
            return monjoPage.Apply(query);
        }

        public static async Task<MonjoFilteredResult<T>> ExecuteAsync<T>(this IMongoQueryable<T> query, MonjoQuery monjoQuery)
        {
            return await query.ExecuteAsync(monjoQuery, typeof(T).Name);
        }

        public static async Task<MonjoFilteredResult<T>> ExecuteAsync<T>(this IMongoQueryable<T> query, MonjoQuery monjoQuery, string collectionName)
        {
            query = query
                    .Apply(monjoQuery.Where, collectionName)
                    .Apply(monjoQuery.Order, collectionName)
                    .AsMongoQueryable();

            var totalCount = await query.CountAsync();
            var pageSize = monjoQuery.Page?.Size ?? totalCount;
            var pageCount = (int)Math.Ceiling(totalCount / (double)pageSize);

            query = query.Apply(monjoQuery.Page).AsMongoQueryable();

            var data = await query.ToListAsync();

            var result = new MonjoFilteredResult<T>
            {
                TotalCount = totalCount,
                PageCount = pageCount,
                Data = data
            };

            return result;
        }

        public static async Task<MonjoFilteredResult<T>> ExecuteAsync<T>(this IMongoQueryable<T> query, MonjoPage monjoPage)
        {
            return await query.ExecuteAsync(new MonjoQuery { Page = monjoPage });
        }
    }
}
