using SLT.Domain.Collections;

namespace SLT.Services._Order.DTOs.Results
{
    public class LockedInvoiceDetailResult : InvoiceResult
    {
        public LockState LockState { get; set; }
        public DateTime? LockedUntilMoment { get; set; }
        public DateTime? ApprovedMoment { get; set; }
        public string ApprovedBy { get; set; } = null;
        public string ApproveHash { get; set; } = null;
        public string BeneficiaryWallet { get; set; } = null;
        public string ResolveHash { get; set; } = null;
        public decimal? StakedPayout { get; set; } = null;
        public string StakedPayoutWei { get; set; } = null;
        public decimal? FeeAmount { get; set; } = null;
        public string FeeAmountWei { get; set; } = null;
        public decimal? PrincipalAmount { get; set; } = null;
        public string PrincipalAmountWei { get; set; } = null;
        public decimal? LivePayoutPreview { get; set; } = null;
        public string LivePayoutPreviewWei { get; set; } = null;
        public decimal? ProfitClaimed { get; set; } = null;
        public string ProfitClaimedWei { get; set; } = null;
        public bool? Approved { get; set; } = null;
        public bool? Settled { get; set; } = null;
        public bool IsCallerAuthorizedApprover { get; set; }
    }
}
