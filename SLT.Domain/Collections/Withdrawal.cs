using Utilities.Attributes;
using Utilities.MongoDatabase.Documents;

namespace SLT.Domain.Collections
{
    [MonjoCollectionName("Withdrawals")]
    public class Withdrawal : BaseDocument
    {
        public string WithdrawalRerefence { get; set; }
        public string StakeReference { get; set; }
        public string WalletAddress { get; set; }
        public string Symbol { get; set; }
        public string Network { get; set; }
        public decimal Amount { get; set; }
        public decimal ProfitAmount { get; set; }
        public decimal Cost { get; set; } = 0;
        public decimal FinalAmount { get; set; }
        public WithdrawalType Type { get; set; }
        public WithdrawalState State { get; set; }
        public string RegisterHash { get; set; }
        public DateTime? RegisterMoment { get; set; } = null;
        public string Hash { get; set; }
    }

    public enum WithdrawalType { StakeProfit, StakeWithdrawal }
    public enum WithdrawalState { NotRegistered, Success, Failed }

}
