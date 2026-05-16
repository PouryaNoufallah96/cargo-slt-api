namespace SLT.Services._Stake.DTOs.Results
{
    public class StakeWalletStatsResult
    {
        public string Name { get; set; }
        public string Symbol { get; set; }

        public decimal TokenAmount { get; set; }
        public int StakeCount { get; set; }
    }
}
