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
    }
}
