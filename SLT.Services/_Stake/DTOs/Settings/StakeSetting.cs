namespace SLT.Services._Stake.DTOs.Settings
{
    public class StakeSetting
    {
        public List<string> AllowedTokensSymbol { get; set; }
        public List<StakePlan> Plans { get; set; }
        public List<EarlyWithdrawRule> EarlyWithdrawRules { get; set; }
    }
     
    public class StakePlan
    {
        public int DurationInMonths { get; set; }
        public decimal MonthlyProfitPercent { get; set; } 
    }

    public class EarlyWithdrawRule
    {
        public int FromMonth { get; set; }
        public int ToMonth { get; set; }
        public decimal MonthlyProfitPercent { get; set; }
    }
}
