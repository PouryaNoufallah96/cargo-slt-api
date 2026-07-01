using SLT.Services._Order.DTOs.Results;
using SLT.Services._Order.DTOs.Updates;
using SLT.Services._TransactionLog.DTOs;

namespace SLT.Services._Order
{
    public interface IOrderService
    {

        Task<OrderFullResult> CreatePendingQuickOrderAsync(CreateQuickInvoiceUpdate update, string walletAddress,string network);
        Task<OrderFullResult> CreatePendingMultiStepOrderAsync(CreateMultiStepOrderUpdate update, string walletAddress,string network);


        Task<OrderListResult> GetOrderListAsync(GetPendingOrderListUpdate update, string walletAddress);
        Task<OrderFullResult> GetOrderDetailAsync(OrderIdUpdate update, string walletAddress);
        Task<OrderTotalReportResult> GetTotalReportAsync(string walletAddress);

        Task<string> DeletePendingOrderAsync(DeletePendingOrderUpdate update,string walletAddress); 

        Task<InvoiceResult> GetInvoiceDetailAsync(InvoiceIdUpdate update, string walletAddress);
        Task<LockedInvoiceDetailResult> GetLockedInvoiceDetailAsync(InvoiceIdUpdate update, string walletAddress);
        Task<bool> SeenWalletAsync(InvoiceIdUpdate update,string walletAddress);


        Task<string> SyncPaidInvoiceAsync(string invoiceId, string payerWallet, string hash);
        Task ActivateNotRegisteredInvoiceAsync(string invoiceId, string hash);
        Task<LockedInvoiceSyncResult> SyncLockedInvoiceCreatedAsync(LockedInvoiceCreatedLog log);
        Task<LockedInvoiceSyncResult> SyncLockedInvoicePaidAsync(LockedInvoicePaidLog log);
        Task<LockedInvoiceSyncResult> SyncLockedInvoiceApprovedAsync(LockedInvoiceApprovedLog log);
        Task<LockedInvoiceSyncResult> SyncLockedInvoiceResolvedAsync(LockedInvoiceResolvedLog log);
        Task RemoveNotRegisteredOrdersAsync(); 
    }
}
//Task<OrderFullResult> CreateQuickOrderAsync(CreateQuickInvoiceUpdate update, string walletAddress);
//Task<OrderFullResult> CreateMultiStepOrderAsync(CreateMultiStepOrderUpdate update, string walletAddress);
