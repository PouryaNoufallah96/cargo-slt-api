using MongoDB.Driver;
using System.Linq.Expressions;
using Utilities.MongoDatabase.Contracts;

namespace Utilities.MongoDatabase.Builders
{
    public class MonjoIndexBuilder<TDocument> : IMonjoIndexBuilder<TDocument>
    {
        IMonjoRepository<TDocument> _monjoRepository;

        IndexKeysDefinition<TDocument> _indexBuilder;

        public MonjoIndexBuilder(IMonjoRepository<TDocument> monjoRepository, IndexKeysDefinition<TDocument> indexKeysDefinition)
        {
            _monjoRepository = monjoRepository;
            _indexBuilder = indexKeysDefinition;
        }

        public IMonjoIndexBuilder<TDocument> AscendingIndex(Expression<Func<TDocument, object>> filterExpression)
        {
            _indexBuilder = _indexBuilder.Ascending(filterExpression);

            return this;
        }

        public IMonjoIndexBuilder<TDocument> DescendingIndex(Expression<Func<TDocument, object>> filterExpression)
        {
            _indexBuilder = _indexBuilder.Descending(filterExpression);

            return this;
        }

        public void Build(CreateIndexOptions options = null)
        {
            var model = new CreateIndexModel<TDocument>(_indexBuilder, options);
            _monjoRepository.CreateIndexOne(model);
        }

        public async Task BuildAsync(CreateIndexOptions options = null)
        {
            var model = new CreateIndexModel<TDocument>(_indexBuilder, options);
            await _monjoRepository.CreateIndexOneAsync(model);
        }
    }
}
