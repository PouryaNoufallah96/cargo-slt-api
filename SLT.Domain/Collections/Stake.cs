using Utilities.Attributes;
using Utilities.MongoDatabase.Documents;

namespace SLT.Domain.Collections
{

    [MonjoCollectionName("Stakes")]
    public class Stake : BaseDocument
    {
        public string StakeReference { get; set; }
        public string WalletAddress { get; set; }
        public string TokenSymbol { get; set; }
        public string TokenName { get; set; }
        public string TokenNetworkName { get; set; }
        public decimal TokenAmount { get; set; }
        public decimal StartAmount { get; set; }
        public decimal TokenPrice { get; set; }
        public decimal EachMonthProfit { get; set; }
        public decimal EachMonthProfitPercent { get; set; }
        public int MonthDuration { get; set; }
        public DateTime StartMoment { get; set; }
        public DateTime EndMoment { get; set; }
        public decimal TotalProfitWithdrawn { get; set; } = 0;
        public decimal TotalCostOfAmountWithdrawn { get; set; } = 0; 
        public decimal TotalProfitOfAmountWithdrawn { get; set; } = 0;  
        public decimal TotalAmountWithdrawn { get; set; } = 0;
        public StakeState State { get; set; }
         
        public string RegisterHash { get; set; }
        public DateTime? RegisterMoment { get; set; } = null;


    }
    public enum StakeState { NotRegistered, Active, Withdraw, Finished, Canceled }

}
