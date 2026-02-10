using SLT.Services._Order.DTOs.Results;
using SLT.Services._Order.DTOs.Updates;

namespace SLT.Services._Order
{
    public interface IOrderService
    {

        Task<OrderFullResult> CreateQuickOrderAsync(CreateQuickInvoiceUpdate update, string walletAddress);
        Task<OrderFullResult> CreateMultiStepOrderAsync(CreateMultiStepOrderUpdate update, string walletAddress);
        Task<OrderListResult> GetOrderListAsync(GetPendingOrderListUpdate update, string walletAddress);
        Task<OrderFullResult> GetOrderDetailAsync(OrderIdUpdate update, string walletAddress);
        Task<OrderTotalReportResult> GetTotalReportAsync(string walletAddress);

        Task<string> DeletePendingOrderAsync(DeletePendingOrderUpdate update,string walletAddress); 

        Task<InvoiceResult> GetInvoiceDetailAsync(InvoiceIdUpdate update, string walletAddress);
        Task<bool> SeenWalletAsync(InvoiceIdUpdate update,string walletAddress);


        Task<string> SyncPaidInvoiceAsync(string invoiceId, string payerWallet, string hash);


    }
}
