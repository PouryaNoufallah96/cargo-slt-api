using MongoDB.Bson.Serialization.Attributes;
using System.Text.Json.Serialization;


namespace Utilities.MongoDatabase.Documents
{
    public class BaseDocument
    {
        [BsonId]
        [BsonRepresentation(MongoDB.Bson.BsonType.ObjectId)]
        public string Id { get; set; }

        public DateTime CreatedMoment { get; set; } = DateTime.UtcNow;
        public DateTime? ModifiedMoment { get; set; } = null;
        [JsonIgnore][BsonDefaultValue(false)] public bool IsDeleted { get; set; }
        [JsonIgnore] public DateTime? DeletedMoment { get; set; } = null;
    }
}
