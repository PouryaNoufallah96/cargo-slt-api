using MongoDB.Bson.Serialization.Attributes;
using Utilities.Attributes;
using Utilities.MongoDatabase.Documents;

namespace SLT.Domain.Collections
{
    [MonjoCollectionName("Users")]
    public class User : BaseDocument
    {
        public string UserPublicKey { get; set; } = Guid.NewGuid().ToString("N");
        public string SecurityStamp { get; set; } = Guid.NewGuid().ToString("N");
        public UserRole Role { get; set; }
        public List<DateTime> LoginDates { get; set; } = [];

        public string WalletAddress { get; set; } // just for customers 


        public string UserName { get; set; } // user for admin
        public string PasswordHash { get; set; } // use for admin
        public List<string> Permissions { get; set; } = []; // for admin
        [BsonDefaultValue(UserStatus.NotVerified)] public UserStatus Status { get; set; }

    }



    public enum UserStatus { Active, Ban, Archived, NotVerified }
    public enum UserRole { Customer, Admin }
}
