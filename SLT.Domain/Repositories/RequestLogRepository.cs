
using SLT.Domain.Collections;
using SLT.Domain.Repositories.Contracts;
using Utilities.MongoDatabase;
using Utilities.MongoDatabase.Contracts;
using static Utilities.Constants.RegisterMode;

namespace SLT.Domain.Repositories
{
    public class RequestLogRepository(IMonjoConnection connection)
        : MonjoRepository<RequestLog>(connection), IRequestLogRepository, ISingletonDependency
    {
    }
}
