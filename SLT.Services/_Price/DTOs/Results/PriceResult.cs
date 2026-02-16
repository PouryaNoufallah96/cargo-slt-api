namespace SLT.Services._Price.DTOs.Results
{
    public class PriceResult
    {
        //public string TokenAddress { get; set; }
        public string TokenName { get; set; }
        public string TokenNetwork { get; set; }
        public decimal Price { get; set; }
        public decimal ChangePrice24hPercentage { get; set; }
        //public decimal LiquidityUsd { get; set; } 
        //public decimal Volume24hUsd { get; set; }
        //public decimal? PoolFeeRate { get; set; }
        //public decimal ReserveBaseUsd { get; set; }
        //public decimal ReserveQuoteUsd { get; set; }
        //public decimal QuoteTokenPriceUsd { get; set; }
    }
}
