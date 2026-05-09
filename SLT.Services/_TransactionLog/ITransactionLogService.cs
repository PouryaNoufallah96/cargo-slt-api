using SLT.Services._TransactionLog.DTOs;
using System.Numerics;

namespace SLT.Services._TransactionLog
{
    public interface ITransactionLogService
    {

        Task CreateInvoiceCreatedAsync(InvoiceCreatedLog log);
        Task CreateInvoicePaidAsync(InvoicePaidLog log);
        Task<BigInteger> GetLastCheckedBlockNumberAsync(string network);


    }
}
