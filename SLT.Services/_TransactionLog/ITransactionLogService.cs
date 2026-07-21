using SLT.Services._TransactionLog.DTOs;
using System.Numerics;

namespace SLT.Services._TransactionLog
{
    public interface ITransactionLogService
    {

        Task CreateInvoiceCreatedAsync(InvoiceCreatedLog log);
        Task CreateInvoicePaidAsync(InvoicePaidLog log);
        Task CreateLockedInvoiceCreatedAsync(LockedInvoiceCreatedLog log);
        Task CreateLockedInvoicePaidAsync(LockedInvoicePaidLog log);
        Task CreateLockedInvoiceApprovedAsync(LockedInvoiceApprovedLog log);
        Task CreateLockedInvoiceResolvedAsync(LockedInvoiceResolvedLog log);
        Task<BigInteger> GetInvoiceLastCheckedBlockNumberAsync(string network);
        //Task<BigInteger> GetCombinedInvoiceEventLastCheckedBlockNumberAsync(string network);



        Task CreateDepositCreatedLogAsync(DepositCreatedLog input);
        //Task CreateEarlyWithdrawnLogAsync(EarlyWithdrawnLog input);
        Task CreateProfitWithdrawnLogAsync(ProfitWithdrawnLog input);
        Task CreateWithdrawnLogAsync(WithdrawnLog input);
        Task<BigInteger> GetDepositLastCheckedBlockNumberAsync(string network = "BEP20");

    }
}
