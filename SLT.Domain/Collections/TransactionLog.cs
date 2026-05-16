using MongoDB.Bson.Serialization.Attributes;
using Utilities.Attributes;
using Utilities.MongoDatabase.Documents;

namespace SLT.Domain.Collections
{
    [MonjoCollectionName("TransactionLogs")]
    public class TransactionLog : BaseDocument
    {
        public string TransactionLogId { get; set; } = Guid.NewGuid().ToString("N");
        public string InvoiceId { get; set; } // use as reference field 
        public string Wallet { get; set; }
        public decimal Amount { get; set; }
        public DateTime? UnlockTime { get; set; } = null;
        public string Hash { get; set; }
        public string TokenAddress { get; set; } = null;
        public decimal BlockNumber { get; set; }
        public BlockchainEventType EventType { get; set; }
        public TransactionStatus Status { get; set; }
        [BsonDefaultValue(null)] public string Data { get; set; } = null;
        public string Network { get; set; }

    }


    public enum TransactionStatus
    {
        Pending,
        Confirmed,
        Failed
    }

    public enum BlockchainEventType
    {
        InvoiceCreated,
        InvoicePaid,
        TransactionConfirmed,
        TransactionFailed,
        BlockMined,
        NetworkStatus,
        DepositCreated,
        EarlyWithdrawn,
        ProfitWithdrawn,
        WithdrawnAll
    }
}
