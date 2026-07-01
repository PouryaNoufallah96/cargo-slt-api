using Nethereum.ABI.FunctionEncoding.Attributes;
using System.Numerics;

namespace SLT.Services._BlockChain._BlockChainWebSocket.DTOs
{

    [Event("InvoiceCreated")]
    public class InvoiceCreatedEventDTO : IEventDTO
    {
        [Parameter("bytes32", "invoiceId", 1, false)]
        public byte[] InvoiceId { get; set; }

        [Parameter("address", "creator", 2, false)]
        public string Creator { get; set; }

        [Parameter("address", "token", 3, false)]
        public string Token { get; set; }

        [Parameter("uint256", "usdAmount", 4, false)]
        public BigInteger UsdAmount { get; set; }

        [Parameter("uint256", "unlockTime", 5, false)]
        public BigInteger UnlockTime { get; set; }
    }

    [Event("InvoicePaid")]
    public class InvoicePaidEventDTO : IEventDTO
    {
        [Parameter("bytes32", "invoiceId", 1, false)]
        public byte[] InvoiceId { get; set; }

        [Parameter("address", "payer", 2, false)]
        public string Payer { get; set; }

        [Parameter("address", "token", 3, false)]
        public string Token { get; set; }

        [Parameter("uint256", "payAmount", 4, false)]
        public BigInteger PayAmount { get; set; }
    }

    [Event("LockedInvoiceCreated")]
    public class LockedInvoiceCreatedEventDTO : IEventDTO
    {
        [Parameter("bytes32", "invoiceId", 1, false)]
        public byte[] InvoiceId { get; set; }

        [Parameter("address", "creator", 2, false)]
        public string Creator { get; set; }

        [Parameter("address", "token", 3, false)]
        public string Token { get; set; }

        [Parameter("uint256", "usdAmount", 4, false)]
        public BigInteger UsdAmount { get; set; }

        [Parameter("uint256", "unlockTime", 5, false)]
        public BigInteger UnlockTime { get; set; }

        [Parameter("uint256", "lockDuration", 6, false)]
        public BigInteger LockDuration { get; set; }

        [Parameter("address", "approver", 7, false)]
        public string Approver { get; set; }
    }

    [Event("LockedInvoicePaid")]
    public class LockedInvoicePaidEventDTO : IEventDTO
    {
        [Parameter("bytes32", "invoiceId", 1, false)]
        public byte[] InvoiceId { get; set; }

        [Parameter("address", "payer", 2, false)]
        public string Payer { get; set; }

        [Parameter("address", "token", 3, false)]
        public string Token { get; set; }

        [Parameter("uint256", "payAmount", 4, false)]
        public BigInteger PayAmount { get; set; }

        [Parameter("uint256", "lockedUntil", 5, false)]
        public BigInteger LockedUntil { get; set; }
    }

    [Event("LockedInvoiceApproved")]
    public class LockedInvoiceApprovedEventDTO : IEventDTO
    {
        [Parameter("bytes32", "invoiceId", 1, false)]
        public byte[] InvoiceId { get; set; }

        [Parameter("address", "approver", 2, false)]
        public string Approver { get; set; }
    }

    [Event("LockedInvoiceResolved")]
    public class LockedInvoiceResolvedEventDTO : IEventDTO
    {
        [Parameter("bytes32", "invoiceId", 1, false)]
        public byte[] InvoiceId { get; set; }

        [Parameter("address", "beneficiary", 2, false)]
        public string Beneficiary { get; set; }

        [Parameter("uint256", "amount", 3, false)]
        public BigInteger Amount { get; set; }

        [Parameter("uint256", "feeAmount", 4, false)]
        public BigInteger FeeAmount { get; set; }
    }


}
