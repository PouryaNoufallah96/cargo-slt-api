using SLT.Domain.Collections;

namespace SLT.Services._Order.DTOs.Results
{
    public class LockedInvoiceSyncResult
    {
        public bool Changed { get; set; }
        public string InvoiceId { get; set; }
        public string OrderId { get; set; }
        public string OwnerWallet { get; set; }
        public string PayerWallet { get; set; }
        public string ApproverWallet { get; set; }
        public string BeneficiaryWallet { get; set; }
        public LockState LockState { get; set; }
        public InvoiceState InvoiceState { get; set; }
        public string ResolveHash { get; set; }
        public string ResolvedAction { get; set; }
        public string NotificationMessage { get; set; }
        public decimal? StakedPayout { get; set; }
        public string StakedPayoutWei { get; set; }
        public decimal? FeeAmount { get; set; }
        public string FeeAmountWei { get; set; }
        public bool? Settled { get; set; }
    }
}
