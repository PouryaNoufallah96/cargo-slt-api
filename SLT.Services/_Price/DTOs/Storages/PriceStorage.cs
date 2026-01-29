using Microsoft.AspNetCore.SignalR;
using SLT.Services._Price._Hubs;
using SLT.Services._Price.DTOs.Results;
using System.Collections.Concurrent;
using static Utilities.Constants.RegisterMode;

namespace SLT.Services._Price.DTOs.Storages
{
    /// <summary>
    /// key is token address 
    /// </summary>
    public class PriceStorage : ConcurrentDictionary<string, TokenPriceData>, ISelfSingletonDependency
    {
        private readonly IHubContext<PriceHub> _hubContext;

        public PriceStorage(IHubContext<PriceHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public void UpdatePrice(string tokenName, PriceResult price)
        {
            AddOrUpdate(
                tokenName,
                new TokenPriceData { Price = price, LastUpdated = DateTime.UtcNow },
                (key, existing) =>
                {
                    existing.Price = price;
                    existing.LastUpdated = DateTime.UtcNow;
                    return existing;
                }
            );

            _hubContext.Clients.All.SendAsync("NotifyPrice", this);
        }

        public TokenPriceData GetPrice(string tokenName)
        {
            TryGetValue(tokenName, out var priceData);
            return priceData;
        }

    }


    public class TokenPriceData
    {
        public PriceResult Price { get; set; }
        public DateTime LastUpdated { get; set; }
    }
}
