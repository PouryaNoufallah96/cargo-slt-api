using MongoDB.Driver.Linq;
using Utilities.MongoDatabase.Filter;

namespace Utilities.MongoDatabase.Extensions
{
    public static class IQueryableExtensions
    {
        public static IQueryable<T> Apply<T>(this IQueryable<T> query, IList<IList<MonjoCondition>> monjoConditions, string collectionName)
        {
            return monjoConditions.Apply(query, collectionName);
        }

        public static IQueryable<T> Apply<T>(this IQueryable<T> query, IList<MonjoOrder> monjoOrders, string collectionName)
        {
            return monjoOrders.Apply(query, collectionName);
        }

        public static IQueryable<T> Apply<T>(this IQueryable<T> query, IList<IList<MonjoCondition>> monjoConditions)
        {
            return monjoConditions.Apply(query, typeof(T).Name);
        }

        public static IQueryable<T> Apply<T>(this IQueryable<T> query, IList<MonjoOrder> monjoOrders)
        {
            return monjoOrders.Apply(query, typeof(T).Name);
        }

        public static IQueryable<T> Apply<T>(this IQueryable<T> query, MonjoPage monjoPage)
        {
            return monjoPage.Apply(query);
        }

        public static MonjoFilteredResult<T> Execute<T>(this IQueryable<T> query, MonjoQuery monjoQuery)
        {
            return query.Execute(monjoQuery, typeof(T).Name);
        }

        public static MonjoFilteredResult<T> Execute<T>(this IQueryable<T> query, MonjoQuery monjoQuery, string collectionName)
        {
            query = query
                    .Apply(monjoQuery.Where, collectionName)
                    .Apply(monjoQuery.Order, collectionName);

            var totalCount = query.Count();
            var pageSize = monjoQuery.Page?.Size ?? totalCount;
            var pageCount = (int)Math.Ceiling(totalCount / (double)pageSize);

            query = query.Apply(monjoQuery.Page);

            var data = query.ToList();

            var result = new MonjoFilteredResult<T>
            {
                TotalCount = totalCount,
                PageCount = pageCount,
                Data = data
            };

            return result;
        }

        public static MonjoFilteredResult<T> Execute<T>(this IQueryable<T> query, MonjoPage monjoPage)
        {
            return query.Execute(new MonjoQuery { Page = monjoPage }, null);
        }
    }
}
