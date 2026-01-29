namespace SLT.Services._Price.DTOs.Results
{
    public class EffectivePriceResult
    {
        public decimal EffectivePrice { get; set; }
        public decimal Price { get; set; }
        public decimal Impact { get; set; }
    }

    public class AssetData
    {
        public decimal BaseTokenPriceUsd { get; set; }
        public decimal BaseTokenPriceNative { get; set; }
        public decimal ReserveInUsd { get; set; }
        public decimal VolumeUsd24h { get; set; }
        public decimal PoolFeePercentage { get; set; }
    }
}
