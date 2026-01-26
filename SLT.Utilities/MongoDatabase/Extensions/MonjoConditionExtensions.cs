using MongoDB.Driver.Linq;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Reflection;
using Utilities.MongoDatabase.Filter;

namespace Utilities.MongoDatabase.Extensions
{
    public static class MonjoConditionExtensions
    {
        public static IMongoQueryable<T> Apply<T>(this IList<IList<MonjoCondition>> Where, IMongoQueryable<T> query,
            string collectionName = null)
        {
            return Where.Apply((IQueryable<T>)query, collectionName).AsMongoQueryable();
        }

        public static IQueryable<T> Apply<T>(this IList<IList<MonjoCondition>> Where, IQueryable<T> query,
            string collectionName = null)
        {
            var parameterExpression = Expression.Parameter(typeof(T), "t");

            MethodCallExpression whereCallExpression = null;

            if (Where != null)
            {
                Expression andAlso = null;

                foreach (var conditions in Where)
                {
                    Expression orElse = null;
                    foreach (var condition in conditions)
                    {
                        if (!string.IsNullOrEmpty(collectionName) &&
                            !condition.Column.StartsWith($"{collectionName}.") &&
                            condition.Column.Contains("."))
                            continue;

                        var columns = condition.Column.Split('.').ToList();
                        if (columns.First() == collectionName) columns.RemoveAt(0);

                        Expression left = parameterExpression;
                        foreach (var member in columns)
                            left = Expression.PropertyOrField(left, member);

                        var type = left.GetMemberType();

                        object rightValue = null;

                        if (type.IsEnum)
                            rightValue = Enum.Parse(type, condition.Operand?.ToString());
                        else
                            rightValue = Convert.ChangeType(condition.Operand?.ToString(), type);

                        Expression right = Expression.Constant(
                            rightValue,
                            type);

                        Expression predicateBody;

                        if (condition.Comparison == ComparisonMethods.Equal)
                            predicateBody = Expression.Equal(left, right);
                        else if (condition.Comparison == ComparisonMethods.NotEqual)
                            predicateBody = Expression.NotEqual(left, right);
                        else if (condition.Comparison == ComparisonMethods.GreaterThan)
                            predicateBody = Expression.GreaterThan(left, right);
                        else if (condition.Comparison == ComparisonMethods.GreaterThanOrEqual)
                            predicateBody = Expression.GreaterThanOrEqual(left, right);
                        else if (condition.Comparison == ComparisonMethods.LessThan)
                            predicateBody = Expression.LessThan(left, right);
                        else if (condition.Comparison == ComparisonMethods.LessThanOrEqual)
                            predicateBody = Expression.LessThanOrEqual(left, right);
                        else if (condition.Comparison == ComparisonMethods.Contains)
                        {
                            MethodInfo method = typeof(string).GetMethod("Contains", new[] { type });
                            predicateBody = Expression.Call(left, method, right);
                        }
                        else if (condition.Comparison == ComparisonMethods.NotContains)
                        {
                            MethodInfo method = typeof(string).GetMethod("Contains", new[] { type });
                            predicateBody = Expression.Not(Expression.Call(left, method, right));
                        }
                        else if (condition.Comparison == ComparisonMethods.IsNull)
                        {
                            right = Expression.Constant(null, typeof(object));
                            predicateBody = Expression.Equal(left, right);
                        }
                        else if (condition.Comparison == ComparisonMethods.IsNotNull)
                        {
                            right = Expression.Constant(null, typeof(object));
                            predicateBody = Expression.NotEqual(left, right);
                        }
                        else if (condition.Comparison == ComparisonMethods.IsEmpty)
                        {
                            right = Expression.Constant(string.Empty, typeof(string));
                            predicateBody = Expression.Equal(left, right);
                        }
                        else if (condition.Comparison == ComparisonMethods.IsNotEmpty)
                        {
                            right = Expression.Constant(string.Empty, typeof(string));
                            predicateBody = Expression.NotEqual(left, right);
                        }
                        else
                            throw new InvalidEnumArgumentException("Unsupported comparison method");

                        if (orElse == null)
                            orElse = predicateBody;
                        else
                            orElse = Expression.OrElse(orElse, predicateBody);
                    }

                    if (andAlso == null)
                        andAlso = orElse;
                    else
                        andAlso = Expression.AndAlso(andAlso, orElse);
                }

                if (andAlso != null)
                    whereCallExpression = Expression.Call(
                        typeof(Queryable),
                        "Where",
                        new Type[] { query.ElementType },
                        query.Expression,
                        Expression.Lambda<Func<T, bool>>(andAlso, new ParameterExpression[] { parameterExpression }));
            }


            if (whereCallExpression != null)
                query = query.Provider.CreateQuery<T>(whereCallExpression);

            return query;
        }
    }
}