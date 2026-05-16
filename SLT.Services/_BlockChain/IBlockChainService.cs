using SLT.Services._BlockChain.DTOs.Updates;
using System.Numerics;

namespace SLT.Services._BlockChain
{
    public interface IBlockChainService
    {


        //Task<string> CreateQuickInvoiceAsync(string id, string tokenAddress, decimal usdtAmount,string ownerAddress);
        //Task<string> CreateMultipleInvoicesAsync(List<CreateMultipleInvoicesUpdate> invoices, string ownerAddress); 

        Task<decimal> GetBEP20WalletAddressSingleTokenBalanceAsync(string walletAddress, string tokenName);
        Task<decimal> GetERC20WalletAddressSingleTokenBalanceAsync(string walletAddress, string tokenName);


        Task<string> DeleteMultipleInvoicesAsync(List<string> invoicesId);  
        Task<string> DeleteERC20MultipleInvoicesAsync(List<string> invoicesId);  
        //Task<string> DeleteSingleInvoiceAsync(string invoiceId);   
         

       // Utility Methods
       decimal ConvertFromWei(BigInteger weiAmount, int decimals = 18);
       BigInteger ConvertToWei(decimal amount, int decimals = 18);


    }
}
