using SLT.Domain.Collections;
using SLT.Domain.Repositories.Contracts;
using Utilities.MongoDatabase;
using Utilities.MongoDatabase.Contracts;
using static Utilities.Constants.RegisterMode;

namespace SLT.Domain.Repositories
{
    public class InvoiceRepository(IMonjoConnection connection) : MonjoRepository<Invoice>(connection), IInvoiceRepository, ISingletonDependency
    {
    }
}
