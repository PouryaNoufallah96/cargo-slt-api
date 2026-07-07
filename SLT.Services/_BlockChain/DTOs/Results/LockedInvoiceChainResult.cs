using System.Numerics;

namespace SLT.Services._BlockChain.DTOs.Results
{
    public class LockedInvoiceChainResult
    {
        public string InvoiceId { get; set; }
        public string Creator { get; set; }
        public string Payer { get; set; }
        public string Token { get; set; }
        public BigInteger UsdAmount { get; set; }
        public BigInteger PayAmount { get; set; }
        public BigInteger UnlockTime { get; set; }
        public bool Paid { get; set; }
        public bool Exists { get; set; }
        public bool IsLocked { get; set; }
        public BigInteger LockDuration { get; set; }
        public string Approver { get; set; }
        public BigInteger LockedUntil { get; set; }
        public BigInteger StakedPayout { get; set; }
        public BigInteger ProfitClaimed { get; set; }
        public bool Approved { get; set; }
        public bool Settled { get; set; }
    }
}
