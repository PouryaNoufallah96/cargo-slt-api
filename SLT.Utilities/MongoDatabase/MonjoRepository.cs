using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using System.Linq.Expressions;
using Utilities.MongoDatabase.Documents;
using Utilities.MongoDatabase.Filter;
using Utilities.MongoDatabase.Contracts;
using Utilities.MongoDatabase.Extensions;
using Utilities.Attributes;
using Utilities.MongoDatabase.Builders;

namespace Utilities.MongoDatabase
{
    public class MonjoRepository<TDocument> : IMonjoRepository<TDocument> where TDocument : BaseDocument
    {
        private readonly IMonjoConnection _connection;

        protected readonly IMongoCollection<TDocument> _collection;

        //private readonly Expression<Func<TDocument, bool>> _defaultCondition;

        public MonjoRepository(IMonjoConnection connection)
        {
            _connection = connection;

            _collection = _connection.Database.GetCollection<TDocument>(CollectionName);
            //_defaultCondition = TDocument => !TDocument.IsDeleted;
            Configure();
        }

        protected virtual void Configure()
        {
        }

        public string CollectionName
        {
            get
            {
                var documentType = typeof(TDocument);
                return ((MonjoCollectionNameAttribute)documentType.GetCustomAttributes(
                        typeof(MonjoCollectionNameAttribute),
                        true)
                    .FirstOrDefault())?.CollectionName;
            }
        }

        public string IdentifierName
        {
            get => MongoCollectionExtensions.GetIdentifierName<TDocument>();
        }

        public bool CollectionExists()
        {
            return _connection.Database.ContainsCollection(CollectionName);
        }

        public virtual IMongoQueryable<TDocument> AsQueryable()
        {
            return _collection.AsQueryable().Where(t => !t.IsDeleted);
        }

        public virtual async Task<IList<BsonDocument>> AggregateAsync(
            PipelineDefinition<TDocument, BsonDocument> pipeline)
        {
            var asyncCursor = await _collection.AggregateAsync(pipeline);

            return await asyncCursor.ToListAsync();
        }

        public virtual IEnumerable<TDocument> FilterBy(
            Expression<Func<TDocument, bool>> filterExpression)
        {
            return _collection.Find(CombineExpressionToDefalutFilter(filterExpression)).ToEnumerable();
        }

        public virtual async Task<IList<TDocument>> FilterByAsync(
            Expression<Func<TDocument, bool>> filterExpression)
        {
            return await _collection.Find(CombineExpressionToDefalutFilter(filterExpression)).ToListAsync();
        }

        public virtual async Task<MonjoFilteredResult<TDocument>> FilterByAsync(MonjoQuery query)
        {
            return await _collection.AsQueryable().ExecuteAsync(query);
        }

        public virtual IEnumerable<TProjected> FilterBy<TProjected>(
            Expression<Func<TDocument, bool>> filterExpression,
            Expression<Func<TDocument, TProjected>> projectionExpression)
        {
            return _collection.Find(CombineExpressionToDefalutFilter(filterExpression)).Project(projectionExpression).ToEnumerable();
        }

        public virtual async Task<IList<TProjected>> FilterByAsync<TProjected>(
            Expression<Func<TDocument, bool>> filterExpression,
            Expression<Func<TDocument, TProjected>> projectionExpression)
        {
            return await _collection.Find(CombineExpressionToDefalutFilter(filterExpression)).Project(projectionExpression).ToListAsync();
        }

        public long Count(Expression<Func<TDocument, bool>> filterExpression)
        {
            return _collection.CountDocuments(CombineExpressionToDefalutFilter(filterExpression));
        }

        public async Task<long> CountAsync(Expression<Func<TDocument, bool>> filterExpression)
        {
            return await _collection.CountDocumentsAsync(CombineExpressionToDefalutFilter(filterExpression));
        }

        public bool Exists(Expression<Func<TDocument, bool>> filterExpression)
        {
            return AsQueryable().Where(filterExpression).Any();
        }

        public async Task<bool> ExistsAsync(Expression<Func<TDocument, bool>> filterExpression)
        {
            return await AsQueryable().Where(filterExpression).AnyAsync();
        }

        public virtual IFindFluent<TDocument, TDocument> Find(
            Expression<Func<TDocument, bool>> filterExpression, int batchSize = 500)
        {
            return _collection.Find(CombineExpressionToDefalutFilter(filterExpression), new FindOptions()
            {
                BatchSize = batchSize
            });
        }

        public virtual Task<IAsyncCursor<TDocument>> FindAsync(
            Expression<Func<TDocument, bool>> filterExpression, int batchSize = 500)
        {
            return _collection.FindAsync(CombineExpressionToDefalutFilter(filterExpression), new FindOptions<TDocument>()
            {
                BatchSize = batchSize
            });
        }

        public virtual IFindFluent<TDocument, TDocument> Find(FilterDefinition<TDocument> filter)
        {
            return _collection.Find(CombineFilterToDefalutFilterDefinition(filter));
        }

        public virtual TDocument FindOne(Expression<Func<TDocument, bool>> filterExpression)
        {
            return _collection.Find(CombineExpressionToDefalutFilter(filterExpression)).FirstOrDefault();
        }

        public virtual Task<TDocument> FindOneAsync(Expression<Func<TDocument, bool>> filterExpression)
        {
            return Task.Run(() => Find(filterExpression).FirstOrDefaultAsync());
        }

        public virtual TDocument FindById(object id)
        {
            var filter = Builders<TDocument>.Filter.Eq(IdentifierName, id);
            return _collection.Find(CombineFilterToDefalutFilterDefinition(filter)).SingleOrDefault();
        }

        public virtual async Task<TDocument> FindByIdAsync(object id)
        {
            var filter = Builders<TDocument>.Filter.Eq(IdentifierName, id);
            return await (await _collection.FindAsync(CombineFilterToDefalutFilterDefinition(filter))).SingleOrDefaultAsync();
        }

        public virtual void InsertOne(TDocument document)
        {
            _collection.InsertOne(document);
        }

        public virtual async Task InsertOneAsync(TDocument document)
        {
            await _collection.InsertOneAsync(document);
        }

        public virtual void InsertMany(IEnumerable<TDocument> documents)
        {
            _collection.InsertMany(documents);
        }

        public virtual async Task InsertManyAsync(IEnumerable<TDocument> documents)
        {
            await _collection.InsertManyAsync(documents);
        }

        public virtual void ReplaceOne(TDocument document)
        {
            var filter = Builders<TDocument>.Filter.Eq(IdentifierName, document.GetIdentifierValue());
            document.ModifiedMoment = DateTime.UtcNow;
            _collection.FindOneAndReplace(CombineFilterToDefalutFilterDefinition(filter), document);
        }

        public virtual async Task ReplaceOneAsync(TDocument document)
        {
            var filter = Builders<TDocument>.Filter.Eq(IdentifierName, document.GetIdentifierValue());
            document.ModifiedMoment = DateTime.UtcNow;
            await _collection.FindOneAndReplaceAsync(CombineFilterToDefalutFilterDefinition(filter), document);
        }

        public virtual async Task ReplaceManyAsync(IEnumerable<ReplaceManyInput<TDocument>> replaceManyInputs)
        {
            var operations = new List<WriteModel<TDocument>>();

            foreach (var replaceManyInput in replaceManyInputs)
            {
                FilterDefinition<TDocument> filter;

                if (replaceManyInput.FilterExpression != null)
                    filter = Builders<TDocument>.Filter.Where(replaceManyInput.FilterExpression);
                else
                    filter = Builders<TDocument>.Filter.Eq(IdentifierName, replaceManyInput.Document.Id);

                var replaceOne = new ReplaceOneModel<TDocument>(filter, replaceManyInput.Document);
                operations.Add(replaceOne);
            }

            if (operations.Count != 0)
                await _collection.BulkWriteAsync(operations, new BulkWriteOptions { IsOrdered = false });
        }

        public virtual void DeleteMany(Expression<Func<TDocument, bool>> filterExpression)
        {
            var update = Builders<TDocument>.Update.Set(q => q.IsDeleted, true).Set(q => q.DeletedMoment, DateTime.UtcNow);
            _collection.UpdateMany(CombineExpressionToDefalutFilter(filterExpression), update);
        }

        public virtual async Task DeleteManyAsync(Expression<Func<TDocument, bool>> filterExpression)
        {
            var update = Builders<TDocument>.Update.Set(q => q.IsDeleted, true).Set(q => q.DeletedMoment, DateTime.UtcNow);
            await _collection.UpdateManyAsync(CombineExpressionToDefalutFilter(filterExpression), update);
        }

        public virtual void DeleteOne(Expression<Func<TDocument, bool>> filterExpression)
        {
            var update = Builders<TDocument>.Update.Set(q => q.IsDeleted, true).Set(q => q.DeletedMoment, DateTime.UtcNow);
            _collection.FindOneAndUpdate(CombineExpressionToDefalutFilter(filterExpression), update);
        }

        public virtual async Task DeleteOneAsync(Expression<Func<TDocument, bool>> filterExpression)
        {
            var update = Builders<TDocument>.Update.Set(q => q.IsDeleted, true).Set(q => q.DeletedMoment, DateTime.UtcNow);
            await _collection.FindOneAndUpdateAsync(CombineExpressionToDefalutFilter(filterExpression), update);
        }


        public virtual void DeleteById(object id)
        {
            var filter = Builders<TDocument>.Filter.Eq(IdentifierName, id);
            var update = Builders<TDocument>.Update.Set(q => q.IsDeleted, true).Set(q => q.DeletedMoment, DateTime.UtcNow);
            _collection.FindOneAndUpdate(CombineFilterToDefalutFilterDefinition(filter), update);
        }

        public virtual async Task DeleteByIdAsync(object id)
        {
            var filter = Builders<TDocument>.Filter.Eq(IdentifierName, id);
            var update = Builders<TDocument>.Update.Set(q => q.IsDeleted, true).Set(q => q.DeletedMoment, DateTime.UtcNow);
            await _collection.FindOneAndUpdateAsync(CombineFilterToDefalutFilterDefinition(filter), update);
        }

        public void CreateIndexOne(CreateIndexModel<TDocument> createIndexModel)
        {
            _collection.Indexes.CreateOne(createIndexModel);
        }

        public async Task CreateIndexOneAsync(CreateIndexModel<TDocument> createIndexModel)
        {
            await _collection.Indexes.CreateOneAsync(createIndexModel);
        }

        public void CreateIndexMany(IEnumerable<CreateIndexModel<TDocument>> createIndexModels)
        {
            _collection.Indexes.CreateMany(createIndexModels);
        }

        public async Task CreateIndexManyAsync(IEnumerable<CreateIndexModel<TDocument>> createIndexModels)
        {
            await _collection.Indexes.CreateManyAsync(createIndexModels);
        }

        public void DropIndexOne(string name)
        {
            _collection.Indexes.DropOne(name);
        }

        public async Task DropIndexOneAsync(string name)
        {
            await _collection.Indexes.DropOneAsync(name);
        }

        public void DropIndexAll()
        {
            _collection.Indexes.DropAll();
        }

        public async Task DropIndexAllAsync()
        {
            await _collection.Indexes.DropAllAsync();
        }

        public IMonjoIndexBuilder<TDocument> AscendingIndex(Expression<Func<TDocument, object>> filterExpression)
        {
            var indexBuilder = new IndexKeysDefinitionBuilder<TDocument>();
            return new MonjoIndexBuilder<TDocument>(this, indexBuilder.Ascending(filterExpression));
        }

        public IMonjoIndexBuilder<TDocument> DescendingIndex(Expression<Func<TDocument, object>> filterExpression)
        {
            var indexBuilder = new IndexKeysDefinitionBuilder<TDocument>();
            return new MonjoIndexBuilder<TDocument>(this, indexBuilder.Descending(filterExpression));
        }

        public async Task<TDocument> FindOneAndUpdateAsync(FilterDefinition<TDocument> filter,
            UpdateDefinition<TDocument> update)
        {
            return await _collection.FindOneAndUpdateAsync(CombineFilterToDefalutFilterDefinition(filter), CombineUpdateToDefalutUpdateDefinition(update));
        }

        public async Task<TDocument> FindOneAndUpdateWithOptionAsync(FilterDefinition<TDocument> filter,UpdateDefinition<TDocument> update,FindOneAndUpdateOptions<TDocument> options = null, CancellationToken cancellationToken = default)
        {
            var combinedFilter = CombineFilterToDefalutFilterDefinition(filter);
            var combinedUpdate = CombineUpdateToDefalutUpdateDefinition(update);

            options ??= new FindOneAndUpdateOptions<TDocument>
            {
                ReturnDocument = ReturnDocument.After 
            };

            return await _collection.FindOneAndUpdateAsync(
                combinedFilter,
                combinedUpdate,
                options,
                cancellationToken
            );
        }

        public TDocument FindOneAndUpdate(FilterDefinition<TDocument> filter, UpdateDefinition<TDocument> update)
        {
            return _collection.FindOneAndUpdate(CombineFilterToDefalutFilterDefinition(filter), CombineUpdateToDefalutUpdateDefinition(update));
        }

        public async Task<UpdateResult> UpdateManyAsync(FilterDefinition<TDocument> filter,
            UpdateDefinition<TDocument> update)
        {
            return await _collection.UpdateManyAsync(CombineFilterToDefalutFilterDefinition(filter), CombineUpdateToDefalutUpdateDefinition(update));
        }

        public virtual async Task UpdateManyAsync(IEnumerable<UpdateManyInput<TDocument>> updateManyInputs)
        {
            var operations = new List<WriteModel<TDocument>>();

            foreach (var input in updateManyInputs)
            {
                var filter = Builders<TDocument>.Filter.Where(input.FilterExpression);
                var updateModel = new UpdateManyModel<TDocument>(filter, input.UpdateDefinition);
                operations.Add(updateModel);
            }

            if (operations.Count > 0)
            {
                await _collection.BulkWriteAsync(operations, new BulkWriteOptions { IsOrdered = false });
            }
        }


        public UpdateResult UpdateMany(FilterDefinition<TDocument> filter, UpdateDefinition<TDocument> update)
        {
            return _collection.UpdateMany(CombineFilterToDefalutFilterDefinition(filter), CombineUpdateToDefalutUpdateDefinition(update));
        }

        public UpdateResult UpsertOne(FilterDefinition<TDocument> filter, UpdateDefinition<TDocument> update)
        {
            return _collection.UpdateOne(CombineFilterToDefalutFilterDefinition(filter), CombineUpdateToDefalutUpdateDefinition(update), new UpdateOptions() { IsUpsert = true });
        }

        public async Task<UpdateResult> UpsertOneAsync(FilterDefinition<TDocument> filter,
            UpdateDefinition<TDocument> update)
        {
            return await _collection.UpdateOneAsync(CombineFilterToDefalutFilterDefinition(filter), CombineUpdateToDefalutUpdateDefinition(update), new UpdateOptions() { IsUpsert = true });
        }

        public UpdateResult UpsertMany(FilterDefinition<TDocument> filter, UpdateDefinition<TDocument> update)
        {
            return _collection.UpdateMany(CombineFilterToDefalutFilterDefinition(filter), CombineUpdateToDefalutUpdateDefinition(update), new UpdateOptions() { IsUpsert = true });
        }

        public async Task<UpdateResult> UpsertManyAsync(FilterDefinition<TDocument> filter,
            UpdateDefinition<TDocument> update)
        {
            return await _collection.UpdateManyAsync(CombineFilterToDefalutFilterDefinition(filter), CombineUpdateToDefalutUpdateDefinition(update), new UpdateOptions() { IsUpsert = true });
        }

        public async Task<TDocument> FindOneAndUpdateAsync(Expression<Func<TDocument, bool>> filter,
            UpdateDefinition<TDocument> update)
        {
            return await _collection.FindOneAndUpdateAsync(CombineExpressionToDefalutFilter(filter), CombineUpdateToDefalutUpdateDefinition(update));
        }

        public TDocument FindOneAndUpdate(Expression<Func<TDocument, bool>> filter, UpdateDefinition<TDocument> update)
        {
            return _collection.FindOneAndUpdate(CombineExpressionToDefalutFilter(filter), CombineUpdateToDefalutUpdateDefinition(update));
        }

        public async Task<UpdateResult> UpdateManyAsync(Expression<Func<TDocument, bool>> filter,
            UpdateDefinition<TDocument> update)
        {
            return await _collection.UpdateManyAsync(CombineExpressionToDefalutFilter(filter), CombineUpdateToDefalutUpdateDefinition(update));
        }

        public UpdateResult UpdateMany(Expression<Func<TDocument, bool>> filter, UpdateDefinition<TDocument> update)
        {
            return _collection.UpdateMany(CombineExpressionToDefalutFilter(filter), CombineUpdateToDefalutUpdateDefinition(update));
        }

        public UpdateResult UpsertOne(Expression<Func<TDocument, bool>> filter, UpdateDefinition<TDocument> update)
        {
            return _collection.UpdateOne(CombineExpressionToDefalutFilter(filter), CombineUpdateToDefalutUpdateDefinition(update), new UpdateOptions() { IsUpsert = true });
        }

        public async Task<UpdateResult> UpsertOneAsync(Expression<Func<TDocument, bool>> filter,
            UpdateDefinition<TDocument> update)
        {
            return await _collection.UpdateOneAsync(CombineExpressionToDefalutFilter(filter), CombineUpdateToDefalutUpdateDefinition(update), new UpdateOptions() { IsUpsert = true });
        }

        public UpdateResult UpsertMany(Expression<Func<TDocument, bool>> filter, UpdateDefinition<TDocument> update)
        {
            return _collection.UpdateMany(CombineExpressionToDefalutFilter(filter), CombineUpdateToDefalutUpdateDefinition(update), new UpdateOptions() { IsUpsert = true });
        }

        public async Task<UpdateResult> UpsertManyAsync(Expression<Func<TDocument, bool>> filter,
            UpdateDefinition<TDocument> update)
        {
            return await _collection.UpdateManyAsync(CombineExpressionToDefalutFilter(filter), CombineUpdateToDefalutUpdateDefinition(update), new UpdateOptions() { IsUpsert = true });
        }

        public virtual async Task RealDeleteManyAsync(Expression<Func<TDocument, bool>> filterExpression)
        {
            await _collection.DeleteManyAsync(filterExpression);
        }


        private static FilterDefinition<TDocument> CombineExpressionToDefalutFilter(Expression<Func<TDocument, bool>> filterExpression)
        {
            Expression<Func<TDocument, bool>> defaultCondition = TDocument => !TDocument.IsDeleted;
            var combinedFilter = Builders<TDocument>.Filter.And(defaultCondition, Builders<TDocument>.Filter.Where(filterExpression));
            return combinedFilter;
        }

        private static FilterDefinition<TDocument> CombineFilterToDefalutFilterDefinition(FilterDefinition<TDocument> filter)
        {
            var defaultFilter = Builders<TDocument>.Filter.Eq(q => q.IsDeleted, false);
            FilterDefinition<TDocument> combinedFilter = defaultFilter & filter;
            return combinedFilter;
        }

        private static UpdateDefinition<TDocument> CombineUpdateToDefalutUpdateDefinition(UpdateDefinition<TDocument> update)
        {
            return update.Set(q => q.ModifiedMoment, DateTime.UtcNow);
        }
    }
    public class ReplaceManyInput<TDocument>
    {
        public TDocument Document { get; set; }
        public Expression<Func<TDocument, bool>> FilterExpression { get; set; }
    }
    public class UpdateManyInput<TDocument>
    {
        public Expression<Func<TDocument, bool>> FilterExpression { get; set; }
        public UpdateDefinition<TDocument> UpdateDefinition { get; set; }
    }

}