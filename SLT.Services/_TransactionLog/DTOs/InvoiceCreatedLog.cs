using SLT.Domain.Collections;
using System.Numerics;

namespace SLT.Services._TransactionLog.DTOs
{
    public class InvoiceCreatedLog
    {
        public string InvoiceId { get; set; }
        public string Creator { get; set; }
        public string Token { get; set; }
        public decimal UsdAmount { get; set; }
        public DateTime? UnLockTime { get; set; }

        public string Address { get; set; }
        public string Hash { get; set; }
        public BigInteger BlockNumber { get; set; }
        public BlockchainEventType EventType { get; set; }

        public string Network { get; set; }
    }

    public class InvoicePaidLog
    {
        public string InvoiceId { get; set; }
        public string Payer { get; set; }
        public string Token { get; set; }
        public decimal PayAmount { get; set; }


        public string Address { get; set; }
        public string Hash { get; set; }
        public BigInteger BlockNumber { get; set; }
        public BlockchainEventType EventType { get; set; }
        public string Network { get; set; }

    }

    public class LockedInvoiceCreatedLog
    {
        public string InvoiceId { get; set; }
        public string Creator { get; set; }
        public string Token { get; set; }
        public BigInteger UsdAmount { get; set; }
        public DateTime? UnLockTime { get; set; }
        public BigInteger LockDuration { get; set; }
        public string Approver { get; set; }

        public string Address { get; set; }
        public string Hash { get; set; }
        public BigInteger BlockNumber { get; set; }
        public BlockchainEventType EventType { get; set; }
        public string Network { get; set; }
    }

    public class LockedInvoicePaidLog
    {
        public string InvoiceId { get; set; }
        public string Payer { get; set; }
        public string Token { get; set; }
        public BigInteger PayAmount { get; set; }
        public DateTime? LockedUntil { get; set; }

        public string Address { get; set; }
        public string Hash { get; set; }
        public BigInteger BlockNumber { get; set; }
        public BlockchainEventType EventType { get; set; }
        public string Network { get; set; }
    }

    public class LockedInvoiceApprovedLog
    {
        public string InvoiceId { get; set; }
        public string Approver { get; set; }

        public string Address { get; set; }
        public string Hash { get; set; }
        public BigInteger BlockNumber { get; set; }
        public BlockchainEventType EventType { get; set; }
        public string Network { get; set; }
    }

    public class LockedInvoiceResolvedLog
    {
        public string InvoiceId { get; set; }
        public string Beneficiary { get; set; }
        public BigInteger Amount { get; set; }
        public BigInteger FeeAmount { get; set; }

        public string Address { get; set; }
        public string Hash { get; set; }
        public BigInteger BlockNumber { get; set; }
        public BlockchainEventType EventType { get; set; }
        public string Network { get; set; }
    }

}
