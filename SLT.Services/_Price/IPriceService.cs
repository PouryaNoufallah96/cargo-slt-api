using SLT.Services._Price.DTOs.Results;

namespace SLT.Services._Price
{
    public interface IPriceService
    {
        Task<PriceResult> FetchTokenPriceAsync(string tokenName);
        Task<PriceResult> FetchSingleTokenPriceAsync(string tokenName);
        Task FetchAllPricesAsync();
        Task<Dictionary<string, PriceResult>> FetchAllPricesForInternalUsageAsync();   
    }
} 
