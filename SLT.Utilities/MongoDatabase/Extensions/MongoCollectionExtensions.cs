using MongoDB.Bson.Serialization.Attributes;

namespace Utilities.MongoDatabase.Extensions
{
    public static class MongoCollectionExtensions
    {
        public static string GetIdentifierName(this object monjoDocument)
            => getIdentifierName(monjoDocument.GetType());

        public static object GetIdentifierValue(this object monjoDocument)
            => getIdentifierValue(monjoDocument, monjoDocument.GetType());

        public static string GetIdentifierName<TDocument>()
            => getIdentifierName(typeof(TDocument));

        private static string getIdentifierName(Type t)
        {
            var properties = t.GetProperties();

            foreach (var property in properties)
            {
                var bsonIdAttribute = (BsonIdAttribute)property
                                    .GetCustomAttributes(typeof(BsonIdAttribute), true)
                                    .FirstOrDefault();

                if (bsonIdAttribute != null)
                    return property.Name;
            }

            throw new KeyNotFoundException("BsonId not found");
        }

        private static object getIdentifierValue(object monjoDocument, Type t)
        {
            var properties = t.GetProperties();

            foreach (var property in properties)
            {
                var bsonIdAttribute = (BsonIdAttribute)property
                                    .GetCustomAttributes(typeof(BsonIdAttribute), true)
                                    .FirstOrDefault();

                if (bsonIdAttribute != null)
                    return property.GetValue(monjoDocument);
            }

            throw new KeyNotFoundException("BsonId not found");
        }
    }
}
