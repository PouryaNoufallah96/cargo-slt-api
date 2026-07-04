using SLT.Domain.Collections;

namespace SLT.Services._Order.DTOs.Results
{
    public class OrderFullResult : OrderResult
    {   
        public List<InvoiceResult> Invoices { get; set; }
        public OwnershipType OwnershipType { get; set; } 
        public bool? IsLocked { get; set; } = null;
    }
    public enum OwnershipType { Owner, Payer }

    public class InvoiceResult
    {
        public DateTime CreatedMoment { get; set; }
        public DateTime? ModifiedMoment { get; set; }
        public string InvoiceId { get; set; } 
        public string OwnerWallet { get; set; }
        public string PayerWallet { get; set; }
        public string OrderId { get; set; }
        public string TokenSymbol { get; set; }
        public string TokenNetwork { get; set; }
        public string TokenAddress { get; set; }
        public decimal USDTAmount { get; set; }
        public string USDTAmountInWei { get; set; }
        public string Desctiption { get; set; }

        public decimal? TokenAmountAtPayment { get; set; }
        public string? TokenAmountWeiAtPayment { get; set; }
        public decimal? TokenPriceAtPayment { get; set; }

        public InvoiceState State { get; set; } = InvoiceState.Pending;
        public DateTime? PayMoment { get; set; } = null;
        public string RegisterHash { get; set; } = null;
        public string PaymentHash { get; set; } = null;
        public DateTime? ActivateDate { get; set; } = null;
        public bool IsLocked { get; set; }
        public int? LockDurationMonths { get; set; }
        public string ApproverWallet { get; set; } = null;
        public OwnershipType OwnershipType { get; set; }
        public LockState? LockState { get; set; } = null;
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
        public bool? IsCallerAuthorizedApprover { get; set; } = null;

    }


}
