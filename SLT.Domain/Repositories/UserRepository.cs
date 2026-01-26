
using SLT.Domain.Collections;
using SLT.Domain.Repositories.Contracts;
using Utilities.MongoDatabase;
using Utilities.MongoDatabase.Contracts;
using static Utilities.Constants.RegisterMode;

namespace SLT.Domain.Repositories
{
    public class UserRepository(IMonjoConnection connection) 
        : MonjoRepository<User>(connection), IUserRepository, ISingletonDependency
    {
        protected override void Configure()
        {
            if (!CollectionExists())
            {
                AscendingIndex(q => q.WalletAddress).Build();
                AscendingIndex(q => q.UserPublicKey).Build();
            }
        }
    }
}
