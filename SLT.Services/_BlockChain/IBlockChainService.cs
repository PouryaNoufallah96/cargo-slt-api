using SLT.Services._BlockChain.DTOs.Updates;
using System.Numerics;

namespace SLT.Services._BlockChain
{
    public interface IBlockChainService
    {


        Task<string> CreateQuickInvoiceAsync(string id, string tokenAddress, decimal usdtAmount);
        Task<string> CreateMultipleInvoicesAsync(List<CreateMultipleInvoicesUpdate> invoices); 
        Task<string> DeleteMultipleInvoicesAsync(List<string> invoicesId);  
        Task<string> DeleteSingleInvoiceAsync(string invoiceId);   

       // Utility Methods
       decimal ConvertFromWei(BigInteger weiAmount, int decimals = 18);
       BigInteger ConvertToWei(decimal amount, int decimals = 18);


    }
}
