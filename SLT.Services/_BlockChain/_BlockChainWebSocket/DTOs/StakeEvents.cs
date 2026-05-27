using Nethereum.ABI.FunctionEncoding.Attributes;
using System.Numerics;

namespace SLT.Services._BlockChain._BlockChainWebSocket.DTOs
{
    [Event("DepositCreated")]
    public class DepositCreatedEventDTO : IEventDTO
    {
        [Parameter("bytes32", "depositId", 1, false)]
        public byte[] DepositId { get; set; }

        [Parameter("address", "depositor", 2, false)]
        public string Depositor { get; set; }

        [Parameter("address", "token", 3, false)]
        public string Token { get; set; }

        [Parameter("uint256", "principal", 4, false)]
        public BigInteger Principal { get; set; }

        [Parameter("uint256", "profit", 5, false)]
        public BigInteger Profit { get; set; }

        [Parameter("uint256", "lockDuration", 6, false)]
        public BigInteger LockDuration { get; set; }

        [Parameter("uint256", "unlocksAt", 7, false)]
        public BigInteger UnlocksAt { get; set; }
    }


    [Event("ProfitWithdrawn")]
    public class ProfitWithdrawnEventDTO : IEventDTO
    {
        [Parameter("bytes32", "depositId", 1, false)]
        public byte[] DepositId { get; set; }

        [Parameter("address", "depositor", 2, false)]
        public string Depositor { get; set; }

        [Parameter("address", "token", 3, false)]
        public string Token { get; set; }

        [Parameter("uint256", "profit", 4, false)]
        public BigInteger Profit { get; set; }
    }

    [Event("Withdrawn")]
    public class WithdrawnEventDTO : IEventDTO
    {
        [Parameter("bytes32", "depositId", 1, false)]
        public byte[] DepositId { get; set; }

        [Parameter("address", "depositor", 2, false)]
        public string Depositor { get; set; }

        [Parameter("uint256", "principal", 3, false)]
        public BigInteger Principal { get; set; }

        [Parameter("uint256", "profit", 4, false)]
        public BigInteger Profit { get; set; }

        [Parameter("uint256", "totalPayout", 5, false)]
        public BigInteger TotalPayout { get; set; }
    }


}
