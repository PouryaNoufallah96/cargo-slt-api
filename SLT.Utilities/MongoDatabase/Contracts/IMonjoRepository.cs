using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using System.Linq.Expressions;
using Utilities.MongoDatabase.Filter;
using Utilities.MongoDatabase;

namespace Utilities.MongoDatabase.Contracts
{
    public interface IMonjoRepository<TDocument>
    {
        IMongoQueryable<TDocument> AsQueryable();

        Task<IList<BsonDocument>> AggregateAsync(PipelineDefinition<TDocument, BsonDocument> pipeline);
        TDocument FindOneAndUpdate(FilterDefinition<TDocument> filter, UpdateDefinition<TDocument> update);
        UpdateResult UpdateMany(FilterDefinition<TDocument> filter, UpdateDefinition<TDocument> update);
        UpdateResult UpsertOne(FilterDefinition<TDocument> filter, UpdateDefinition<TDocument> update);
        UpdateResult UpsertMany(FilterDefinition<TDocument> filter, UpdateDefinition<TDocument> update);
        TDocument FindOneAndUpdate(Expression<Func<TDocument, bool>> filter, UpdateDefinition<TDocument> update);
        UpdateResult UpdateMany(Expression<Func<TDocument, bool>> filter, UpdateDefinition<TDocument> update);
        UpdateResult UpsertOne(Expression<Func<TDocument, bool>> filter, UpdateDefinition<TDocument> update);
        UpdateResult UpsertMany(Expression<Func<TDocument, bool>> filter, UpdateDefinition<TDocument> update);
        Task<TDocument> FindOneAndUpdateAsync(FilterDefinition<TDocument> filter, UpdateDefinition<TDocument> update);
        Task<TDocument> FindOneAndUpdateWithOptionAsync(FilterDefinition<TDocument> filter, UpdateDefinition<TDocument> update,
            FindOneAndUpdateOptions<TDocument> options = null, CancellationToken cancellationToken = default);
        Task<UpdateResult> UpdateManyAsync(FilterDefinition<TDocument> filter, UpdateDefinition<TDocument> update);
        Task<UpdateResult> UpsertOneAsync(FilterDefinition<TDocument> filter, UpdateDefinition<TDocument> update);
        Task<UpdateResult> UpsertManyAsync(FilterDefinition<TDocument> filter, UpdateDefinition<TDocument> update);
        Task<TDocument> FindOneAndUpdateAsync(Expression<Func<TDocument, bool>> filter, UpdateDefinition<TDocument> update);
        Task<UpdateResult> UpdateManyAsync(Expression<Func<TDocument, bool>> filter, UpdateDefinition<TDocument> update);
        Task<UpdateResult> UpsertOneAsync(Expression<Func<TDocument, bool>> filter, UpdateDefinition<TDocument> update);
        Task<UpdateResult> UpsertManyAsync(Expression<Func<TDocument, bool>> filter, UpdateDefinition<TDocument> update);
        IEnumerable<TDocument> FilterBy(
            Expression<Func<TDocument, bool>> filterExpression);
        Task<IList<TDocument>> FilterByAsync(
            Expression<Func<TDocument, bool>> filterExpression);
        Task<MonjoFilteredResult<TDocument>> FilterByAsync(MonjoQuery query);
        IEnumerable<TProjected> FilterBy<TProjected>(
            Expression<Func<TDocument, bool>> filterExpression,
            Expression<Func<TDocument, TProjected>> projectionExpression);
        Task<IList<TProjected>> FilterByAsync<TProjected>(
            Expression<Func<TDocument, bool>> filterExpression,
            Expression<Func<TDocument, TProjected>> projectionExpression);
        long Count(Expression<Func<TDocument, bool>> filterExpression);

        Task<long> CountAsync(Expression<Func<TDocument, bool>> filterExpression);

        bool Exists(Expression<Func<TDocument, bool>> filterExpression);

        Task<bool> ExistsAsync(Expression<Func<TDocument, bool>> filterExpression);


        IFindFluent<TDocument, TDocument> Find(Expression<Func<TDocument, bool>> filterExpression, int batchSize = 500);

        Task<IAsyncCursor<TDocument>> FindAsync(Expression<Func<TDocument, bool>> filterExpression, int batchSize = 500);

        //IFindFluent<BsonDocument, BsonDocument> Find(FilterDefinition<BsonDocument> filter);

        IFindFluent<TDocument, TDocument> Find(FilterDefinition<TDocument> filter);

        TDocument FindOne(Expression<Func<TDocument, bool>> filterExpression);

        Task<TDocument> FindOneAsync(Expression<Func<TDocument, bool>> filterExpression);

        TDocument FindById(object id);

        Task<TDocument> FindByIdAsync(object id);

        void InsertOne(TDocument document);

        Task InsertOneAsync(TDocument document);

        void InsertMany(IEnumerable<TDocument> documents);

        Task InsertManyAsync(IEnumerable<TDocument> documents);

        void ReplaceOne(TDocument document);

        Task ReplaceOneAsync(TDocument document);
        Task ReplaceManyAsync(IEnumerable<ReplaceManyInput<TDocument>> replaceManyInputs);

        void DeleteOne(Expression<Func<TDocument, bool>> filterExpression);

        Task DeleteOneAsync(Expression<Func<TDocument, bool>> filterExpression);

        void DeleteById(object id);

        Task DeleteByIdAsync(object id);

        void DeleteMany(Expression<Func<TDocument, bool>> filterExpression);

        Task DeleteManyAsync(Expression<Func<TDocument, bool>> filterExpression);

        void CreateIndexOne(CreateIndexModel<TDocument> createIndexModel);

        Task CreateIndexOneAsync(CreateIndexModel<TDocument> createIndexModel);

        void CreateIndexMany(IEnumerable<CreateIndexModel<TDocument>> createIndexModels);

        Task CreateIndexManyAsync(IEnumerable<CreateIndexModel<TDocument>> createIndexModels);

        void DropIndexOne(string name);
        Task DropIndexOneAsync(string name);

        void DropIndexAll();
        Task DropIndexAllAsync();

        Task RealDeleteManyAsync(Expression<Func<TDocument, bool>> filterExpression);

        IMonjoIndexBuilder<TDocument> AscendingIndex(Expression<Func<TDocument, object>> filterExpression);
        IMonjoIndexBuilder<TDocument> DescendingIndex(Expression<Func<TDocument, object>> filterExpression);
    }
}
