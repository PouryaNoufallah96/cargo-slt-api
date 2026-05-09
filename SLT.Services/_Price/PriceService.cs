using Utilities.Exceptions.Common;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using SLT.Services._Price.DTOs.Results;
using SLT.Services._Price.DTOs.Settings;
using SLT.Services._Price.DTOs.Storages;
using System.Collections.Concurrent;
using System.Text.Json;
using static Utilities.Constants.RegisterMode;

namespace SLT.Services._Price
{
    public class PriceService(
        AvailableTokensSettings _availableTokenDatas,
        CallPriceSettings _callPriceSettings,
       ILogger<PriceService> logger,
        PriceStorage _priceStorage) : IPriceService, IScopedDependency
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private readonly ILogger<PriceService> _logger = logger;
        private decimal _cachedBnbPrice = 300m;
        private DateTime _lastBnbPriceUpdate = DateTime.MinValue;

        public async Task<PriceResult> FetchTokenPriceForShieldAsync(string tokenName)
        {
            var priceData = _priceStorage.GetPrice(tokenName.ToUpper());
            if (priceData != null && priceData.Price != null) return priceData.Price;
            return  await FetchTokenPriceAsync(tokenName.ToUpper());
        }

        public async Task<PriceResult> FetchTokenPriceAsync(string tokenName)
        {
            try
            {
                return await FetchTokenPriceFromGeckoTerminalAsync(tokenName);
            }
            catch (Exception ex)
            {
                return await FetchTokenPriceFromGeckoTerminalAsync(tokenName);
            }
        }

        public async Task FetchAllPricesAsync()
        {
            foreach (var token in _availableTokenDatas)
            {
                var priceData = await FetchTokenPriceAsync(token.Name);
                if (priceData != null)
                {
                    _priceStorage.UpdatePrice(token.Name, priceData);
                }

                await Task.Delay(500); 
            }
        }
        //public async Task FetchAllPricesAsync()
        //{
        //    var tasks = _availableTokenDatas.Select(async token =>
        //    {
        //        var priceData = await FetchTokenPriceAsync(token.Name);
        //        if (priceData != null)
        //        {
        //            _priceStorage.UpdatePrice(token.Name, priceData);
        //        }
        //    });

        //    await Task.WhenAll(tasks);
        //}

        public async Task<Dictionary<string, PriceResult>> FetchAllPricesForInternalUsageAsync()
        {
            var result = new ConcurrentDictionary<string, PriceResult>();

            var tasks = _availableTokenDatas.Select(async token =>
            {
                var priceData = await FetchTokenPriceAsync(token.Name);
                if (priceData != null)
                {
                    result[token.Name] = priceData;
                }
            });

            await Task.WhenAll(tasks);

            return result.ToDictionary(kv => kv.Key, kv => kv.Value);
        }

        /// <summary>
        /// this method use for fetch price data with token name
        /// </summary>
        /// <param name="tokenName"></param>
        /// <param name="poolId"></param>
        /// <returns></returns>
        public async Task<PriceResult> FetchTokenPriceFromGeckoTerminalAsync(string tokenName, string poolId = null)
        {
            var pool = poolId == null ? _availableTokenDatas.FirstOrDefault(q => q.Name == tokenName.ToUpper()).PoolId : poolId;

            string url = $"https://api.geckoterminal.com/api/v2/networks/bsc/pools/{pool}";

            if(pool == "0x935dce0d9cbbf2915cc4b5ec6bfd665e450c3854")
            {
                url = $"https://api.geckoterminal.com/api/v2/networks/eth/pools/{pool}";
            }

            try
            {
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var jsonString = await response.Content.ReadAsStringAsync();
                using JsonDocument doc = JsonDocument.Parse(jsonString);

                var attributes = doc.RootElement
                    .GetProperty("data")
                    .GetProperty("attributes");

                decimal basePrice = decimal.Parse(attributes.GetProperty("base_token_price_usd").GetString()!);
                decimal quotePrice = decimal.Parse(attributes.GetProperty("quote_token_price_usd").GetString()!);

                decimal liquidityUsd = decimal.Parse(attributes.GetProperty("reserve_in_usd").GetString()!);
                decimal volume24h = decimal.Parse(attributes.GetProperty("volume_usd").GetProperty("h24").GetString()!);
                decimal changePrice24h = decimal.Parse(attributes.GetProperty("price_change_percentage").GetProperty("h24").GetString()!);
                decimal? poolFee = attributes.TryGetProperty("pool_fee_percentage", out var feeProp) && feeProp.ValueKind != JsonValueKind.Null
                                   ? decimal.Parse(feeProp.GetString()!)
                                   : null;

                return new PriceResult
                {
                    TokenName = tokenName,
                    TokenNetwork = "BSC",
                    Price = basePrice,
                    ChangePrice24hPercentage = changePrice24h,
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching token price gecko for {poolId}: {ex.Message}");
                return null;
            }
        }

       
    }

}
