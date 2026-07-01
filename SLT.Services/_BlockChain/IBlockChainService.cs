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

        Task<BigInteger> PreviewAccruedProfitAsync(string depositId, string network);
        Task<LockedInvoiceChainResult> GetLockedInvoiceAsync(string invoiceId, string network);



       // Utility Methods
       decimal ConvertFromWei(BigInteger weiAmount, int decimals = 18);
       BigInteger ConvertToWei(decimal amount, int decimals = 18);


    }

    public class LockedInvoiceChainResult
    {
        public string InvoiceId { get; set; }
        public string Creator { get; set; }
        public string Payer { get; set; }
        public string Token { get; set; }
        public BigInteger UsdAmount { get; set; }
        public BigInteger PayAmount { get; set; }
        public BigInteger UnlockTime { get; set; }
        public BigInteger LockDuration { get; set; }
        public string Approver { get; set; }
        public BigInteger LockedUntil { get; set; }
        public BigInteger StakedPayout { get; set; }
        public BigInteger ProfitClaimed { get; set; }
        public bool Approved { get; set; }
        public bool Settled { get; set; }
    }
}
