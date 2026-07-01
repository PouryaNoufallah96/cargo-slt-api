using MongoDB.Bson.Serialization.Attributes;
using Utilities.Attributes;
using Utilities.MongoDatabase.Documents;

namespace SLT.Domain.Collections
{

    [MonjoCollectionName("Invoices")] 
    public class Invoice : BaseDocument
    {
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
        [BsonDefaultValue(null)] public string RemoveHash { get; set; } = null;
        public DateTime? ActivateDate { get; set; } = null; //date only
        public LockDetail Lock { get; set; } = null;
        public List<string> Errors { get; set; } = null;

    }

    public class LockDetail
    {
        public int DurationMonths { get; set; }
        public string ApproverWallet { get; set; } = null;
        public DateTime? LockedUntilMoment { get; set; } = null;
        public DateTime? ApprovedMoment { get; set; } = null;
        public string ApprovedBy { get; set; } = null;
        public string BeneficiaryWallet { get; set; } = null;
        public decimal? StakedPayout { get; set; } = null;
        public string StakedPayoutWei { get; set; } = null;
        public decimal? FeeAmount { get; set; } = null;
        public string FeeAmountWei { get; set; } = null;
        public string ApproveHash { get; set; } = null;
        public string ResolveHash { get; set; } = null;
        public LockState State { get; set; } = LockState.Created;

        public decimal? LivePayoutPreview { get; set; } = null;
        public string LivePayoutPreviewWei { get; set; } = null;
        public decimal? ProfitClaimed { get; set; } = null;
        public string ProfitClaimedWei { get; set; } = null;
        public bool? Approved { get; set; } = null;
        public bool? Settled { get; set; } = null;
    }

    public enum LockState
    {
        Created,
        Funded,
        Approved,
        Released,
        Refunded
    }


     
    public enum InvoiceState
    {
        Pending,
        Completed,
        Failed,
        Expired,
        Cancelled,
        NotRegistered,
        Locked,
        Refunded
    }

}
