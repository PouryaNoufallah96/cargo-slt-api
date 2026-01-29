using SLT.Domain.Collections;
using Utilities.MongoDatabase.Contracts;

namespace SLT.Domain.Repositories.Contracts
{
    public interface IInvoiceRepository : IMonjoRepository<Invoice>
    {
    }
}
