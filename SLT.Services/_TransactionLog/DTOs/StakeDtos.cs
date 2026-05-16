using SLT.Domain.Collections;
using System.Numerics;

namespace SLT.Services._TransactionLog.DTOs
{
    public class DepositCreatedLog
    {
        public string DepositId { get; set; }
        public string Address { get; set; }
        public string Hash { get; set; }
        public BigInteger BlockNumber { get; set; }
        public BlockchainEventType EventType { get; set; }
        public string Network { get; set; }
        public string Token { get; set; }
        public string Depositor { get; set; }
        public BigInteger Principal { get; set; }
        public BigInteger Profit { get; set; }
        public BigInteger LockDuration { get; set; }
        public BigInteger UnlocksAt { get; set; }
    }

    public class DepositCreatedData
    {
        public string Depositor { get; set; }
        public BigInteger Principal { get; set; }
        public BigInteger Profit { get; set; }
        public BigInteger LockDuration { get; set; }
        public BigInteger UnlocksAt { get; set; }
    }


    public class EarlyWithdrawnLog
    {
        public string DepositId { get; set; }
        public string Address { get; set; }
        public string Hash { get; set; }
        public BigInteger BlockNumber { get; set; }
        public BlockchainEventType EventType { get; set; }
        public string Network { get; set; }

        public string Depositor { get; set; }

        public BigInteger WithdrawAmount { get; set; }
        public BigInteger ProfitAmount { get; set; }
        public BigInteger ClaimedProfitAmount { get; set; }
        public BigInteger FinalPayoutAmount { get; set; }
    }

    public class EarlyWithdrawnData
    {
        public string Depositor { get; set; }

        public BigInteger WithdrawAmount { get; set; }
        public BigInteger ProfitAmount { get; set; }
        public BigInteger ClaimedProfitAmount { get; set; }
        public BigInteger FinalPayoutAmount { get; set; }
    }



    public class ProfitWithdrawnLog
    {
        public string DepositId { get; set; }
        public string Address { get; set; }
        public string Hash { get; set; }
        public BigInteger BlockNumber { get; set; }
        public BlockchainEventType EventType { get; set; }
        public string Network { get; set; }
        public string Token { get; set; }
        public BigInteger Profit { get; set; }
        public string Depositor { get; set; }

    }
    public class ProfitWithdrawnData
    {
        public BigInteger Profit { get; set; }
        public string Depositor { get; set; }

    }



    public class WithdrawnLog
    {
        public string DepositId { get; set; }
        public string Address { get; set; }
        public string Hash { get; set; }
        public BigInteger BlockNumber { get; set; }
        public BlockchainEventType EventType { get; set; }
        public string Network { get; set; }

        public string Depositor { get; set; }

        public BigInteger Principal { get; set; }
        public BigInteger Profit { get; set; }
        public BigInteger TotalPayout { get; set; }
    }

    public class WithdrawnData
    {
        public string Depositor { get; set; }

        public BigInteger Principal { get; set; }
        public BigInteger Profit { get; set; }
        public BigInteger TotalPayout { get; set; }
    }





















}
