using SLT.Domain.Collections;
using Utilities.MongoDatabase.Contracts;

namespace SLT.Domain.Repositories.Contracts
{
    public interface IOrderRepository : IMonjoRepository<Order>
    {
    }
}
