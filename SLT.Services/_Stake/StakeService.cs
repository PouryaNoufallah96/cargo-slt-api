using Autofac.Features.OwnedInstances;
using Microsoft.AspNetCore.SignalR;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using Nethereum.Model;
using SLT.Domain.Collections;
using SLT.Domain.Repositories.Contracts;
using SLT.Services._BlockChain;
using SLT.Services._Price;
using SLT.Services._Price.DTOs.Settings;
using SLT.Services._Stake.DTOs.Results;
using SLT.Services._Stake.DTOs.Settings;
using SLT.Services._Stake.DTOs.Updates;
using SLT.Services._TransactionLog._Hub;
using SLT.Services._Withdrawal.DTOs.Results;
using System.Security.Cryptography;
using Utilities.Exceptions.Common;
using Utilities.Utilities;
using static Utilities.Constants.RegisterMode;

namespace SLT.Services._Stake
{
    public class StakeService(IStakeRepository _stakeRepository,
        StakeSetting _stakeSetting,
        AvailableTokensSettings _availableTokenData,
        IPriceService _priceService,
        IHubContext<WalletNotifyHub> _hubContext,
        IWithdrawalRepository _withdrawalRepository,
        IBlockChainService _blockChainService)
        : IStakeService, IScopedDependency
    {

        /// <summary>
        /// use for create a new stake
        /// </summary>
        /// <param name="update"></param>
        /// <param name="publicKey"></param>
        /// <param name="evmWalletAddress"></param>
        /// <returns></returns>
        /// <exception cref="BadRequestException"></exception>
        public async Task<StakeResult> CreateStakeAsync(CreateStakeUpdate update, string walletAddress, string walletNetwork)
        {
            if (update == null)
                throw new BadRequestException("Request body is required");

            var network = walletNetwork.ToUpper();

            var symbol = update.Symbol.Trim().ToUpper();
            var tokenData = ValidateToken(symbol);
            if (tokenData.Network != network) throw new BadRequestException($"Please Sign with {tokenData.Network} Network with your wallet");



            if (!_stakeSetting.AllowedTokensSymbol
                .Any(s => s.Equals(symbol, StringComparison.OrdinalIgnoreCase)))
            {
                throw new BadRequestException($"Token '{update.Symbol}' is not allowed for staking");
            }

            var plan = _stakeSetting.Plans
                .FirstOrDefault(p => p.DurationInMonths == update.Duration)
                ?? throw new BadRequestException("Invalid staking duration. Allowed durations are based on configured plans 12 and 24 month");

            var tokenBalance = 0m;

            if (network == "BEP20")
            {
                tokenBalance = await _blockChainService.GetBEP20WalletAddressSingleTokenBalanceAsync(walletAddress, symbol);
            }
            else
            {
                tokenBalance = await _blockChainService.GetERC20WalletAddressSingleTokenBalanceAsync(walletAddress, symbol);

            }

            if (tokenBalance < update.Amount)
                throw new BadRequestException($"Insufficient {symbol} balance!");


            var eachMonthProfit = update.Amount * (plan.MonthlyProfitPercent / 100);

            var start = DateTime.UtcNow;
            var end = start.AddMonths(update.Duration);

            var reference = GenerateBytes32HexId();
            var tokenPrice = await _priceService.FetchSingleTokenPriceAsync(symbol);

            var stake = new Stake
            {
                StakeReference = reference,
                WalletAddress = walletAddress,
                TokenSymbol = symbol,
                TokenAmount = update.Amount,
                StartAmount = update.Amount,
                TokenPrice = tokenPrice.Price,
                TokenName = tokenData.PoolName,
                EachMonthProfitPercent = plan.MonthlyProfitPercent,
                EachMonthProfit = eachMonthProfit,
                MonthDuration = update.Duration,
                StartMoment = start,
                EndMoment = end,
                TokenNetworkName = network,
                TotalProfitWithdrawn = 0,
                State = StakeState.NotRegistered
            };

            await _stakeRepository.InsertOneAsync(stake);
            return ConvertToResult(stake);
        }


        /// <summary>
        /// use for get user stake history
        /// </summary>
        /// <param name="update"></param>
        /// <param name="publicKey"></param>
        /// <param name="evmWalletAddress"></param>
        /// <returns></returns>
        public async Task<StakeListResult> GetStakeHistoryAsync(StakeHistoryUpdate update, string evmWalletAddress)
        {
            var query = _stakeRepository.AsQueryable().Where(q => q.State != StakeState.NotRegistered);

            if (update.Symbol != null && update.Symbol.HasValue())
            {
                query = query.Where(q => q.TokenSymbol == update.Symbol.ToUpper());
            }

            query = query.Where(x =>
                x.WalletAddress == evmWalletAddress);

            var totalCount = await query.CountAsync();

            var page = update.Pagination?.Page ?? 1;
            var size = update.Pagination?.Size ?? 25;

            var data = await query
                .OrderByDescending(x => x.CreatedMoment)
                .Skip((page - 1) * size)
                .Take(size)
                .ToListAsync();

            var result = new StakeListResult
            {
                TotalCount = totalCount,
                PageCount = (int)Math.Ceiling((double)totalCount / size),
                Data = data.Select(ConvertToResult).ToList()
            };

            return result;
        }


        /// <summary>
        /// use for get wallet stats for stake section
        /// </summary>
        /// <param name="update"></param>
        /// <param name="publicKey"></param>
        /// <param name="evmWalletAddress"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public async Task<List<StakeWalletStatsResult>> GetWalletStatsAsync(GetStakeWalletStatsUpdate update, string evmWalletAddress)
        {
            var query = _stakeRepository.AsQueryable().Where(q => q.State != StakeState.NotRegistered);

            query = query.Where(x =>
                x.WalletAddress == evmWalletAddress);

            if (update.ShouldGrouped)
            {
                var grouped = await query
                    .GroupBy(x => x.TokenSymbol)
                    .Select(g => new StakeWalletStatsResult
                    {
                        Symbol = g.Key,
                        Name = g.First().TokenName,

                        TokenAmount = g.Sum(x => x.TokenAmount),
                        StakeCount = g.Count()
                    })
                    .ToListAsync();

                return grouped;
            }

            var list = await query
                .Select(x => new StakeWalletStatsResult
                {
                    Symbol = x.TokenSymbol,
                    Name = x.TokenName,

                    TokenAmount = x.TokenAmount,
                    StakeCount = 1
                })
                .ToListAsync();

            return list;
        }


        /// <summary>
        /// use for get stake detail
        /// </summary>
        /// <param name="update"></param>
        /// <param name="publicKey"></param>
        /// <param name="evmWalletAddress"></param>
        /// <returns></returns>
        /// <exception cref="NotFoundException"></exception>
        /// <exception cref="BadRequestException"></exception>
        public async Task<StakeDetailResult> GetStakeDetailAsync(StakeDetailUpdate update, string evmWalletAddress)
        {

            var stake = await _stakeRepository.AsQueryable()
                .FirstOrDefaultAsync(q => q.StakeReference == update.StakeReference)
                ?? throw new NotFoundException("Stake not found!");

            if (stake.WalletAddress != evmWalletAddress)
                throw new BadRequestException("Access denied");

            var result = ConvertToDetailResult(stake);

            var now = DateTime.UtcNow;

            var lastStakeWithdrawal = await _withdrawalRepository.AsQueryable()
                .Where(w =>
                    w.StakeReference == stake.StakeReference &&
                    w.Type == WithdrawalType.StakeWithdrawal &&
                    w.State == WithdrawalState.Success)
                .OrderByDescending(w => w.RegisterMoment)
                .FirstOrDefaultAsync();

            var profitStartDate = lastStakeWithdrawal?.RegisterMoment ?? stake.StartMoment;

            var passedMonths = GetPassedFullMonths(profitStartDate, now);
            decimal availableProfit = 0;

            if (passedMonths > 0)
            {
                var monthlyProfit =
                    stake.TokenAmount * (stake.EachMonthProfitPercent / 100m);

                availableProfit = monthlyProfit * passedMonths;
            }

            result.AvailableProfitForWithdraw = availableProfit;


            var withdrawals = await _withdrawalRepository.AsQueryable()
                .Where(w => w.StakeReference == stake.StakeReference)
                .OrderByDescending(w => w.CreatedMoment)
                .ToListAsync();

            result.Withrawals = withdrawals.Select(w => new WithdrawalResult
            {
                WithdrawalRerefence = w.WithdrawalRerefence,
                WalletAddress = w.WalletAddress,
                Symbol = w.Symbol,
                Network = w.Network,
                Amount = w.Amount,
                ProfitAmount = w.ProfitAmount,
                Cost = w.Cost,
                FinalAmount = w.FinalAmount,
                Type = w.Type,
                State = w.State,
                CreatedMoment = w.CreatedMoment,
                ModifiedMoment = w.ModifiedMoment
            }).ToList();

            return result;
        }

        /// <summary>
        /// use for activate stake
        /// </summary>
        /// <param name="depositRef"></param>
        /// <param name="hash"></param>
        /// <returns></returns>
        public async Task ActivateStakeAsync(string depositRef, string hash)
        {
            var filter = Builders<Stake>.Filter.And(
                Builders<Stake>.Filter.Eq(x => x.StakeReference, depositRef),
                Builders<Stake>.Filter.Eq(x => x.State, StakeState.NotRegistered)
            );

            var update = Builders<Stake>.Update
                .Set(x => x.RegisterHash, hash)
                .Set(x => x.RegisterMoment, DateTime.UtcNow)
                .Set(x => x.State, StakeState.Active);

            var options = new FindOneAndUpdateOptions<Stake>
            {
                ReturnDocument = ReturnDocument.After
            };

            var stake = await _stakeRepository.FindOneAndUpdateWithOptionAsync(filter, update, options);

            if (stake == null)
                return;

            var shortHash = hash[..10];

            await _hubContext.Clients
                .Group(stake.WalletAddress)
                .SendAsync(
                    "PaymentMessage",
                    $"Your Deposit Activated {stake.TokenSymbol}: {shortHash}"
                );
        }


        /// <summary>
        /// use for remove not registered stakes 
        /// </summary>
        /// <returns></returns>
        public async Task RemoveNotRegisteredStakesAsync()
        {
            var oneWeekAgo = DateTime.UtcNow.AddDays(-7);
            var query = _stakeRepository.AsQueryable();
            var stakesToDelete = await query
                .Where(x =>
                    x.RegisterHash == null &&
                    x.State == StakeState.NotRegistered &&
                    x.CreatedMoment <= oneWeekAgo)
                .ToListAsync();

            if (stakesToDelete == null || stakesToDelete.Count == 0)
                return;

            var ids = stakesToDelete.Select(x => x.Id).ToList();

            await _stakeRepository.DeleteManyAsync(x => ids.Contains(x.Id));
        }




        /// <summary>
        /// use for getting passed full months
        /// </summary>
        /// <param name="start"></param>
        /// <param name="now"></param>
        /// <returns></returns>
        private int GetPassedFullMonths(DateTime start, DateTime now)
        {
            int months = (now.Year - start.Year) * 12 + (now.Month - start.Month);

            if (now.Day < start.Day)
                months--;

            return Math.Max(0, months);
        }


        /// <summary>
        /// convertor
        /// </summary>
        /// <param name="stake"></param>
        /// <returns></returns>
        private StakeResult ConvertToResult(Stake stake)
        {

            return new StakeResult
            {
                StakeReference = stake.StakeReference,
                WalletAddress = stake.WalletAddress,
                TokenSymbol = stake.TokenSymbol,
                TokenName = stake.TokenName,
                TokenAmount = stake.TokenAmount,
                TokenPrice = stake.TokenPrice,
                MonthDuration = stake.MonthDuration,
                StartMoment = stake.StartMoment,
                EndMoment = stake.EndMoment,
                TotalProfitWithdrawn = stake.TotalProfitWithdrawn,
                TotalAmountWithdrawn = stake.TotalAmountWithdrawn,
                TotalCostOfAmountWithdrawn = stake.TotalCostOfAmountWithdrawn,
                TotalProfitOfAmountWithdrawn = stake.TotalProfitOfAmountWithdrawn,
                State = stake.State,
                EachMonthProfit = stake.EachMonthProfit,
                EachMonthProfitPercent = stake.EachMonthProfitPercent,
                CreatedMoment = stake.CreatedMoment,
                ModifiedMoment = stake.ModifiedMoment,
            };

        }
        
        private StakeDetailResult ConvertToDetailResult(Stake stake)
        {

            return new StakeDetailResult
            {
                StakeReference = stake.StakeReference,
                WalletAddress = stake.WalletAddress,
                TokenSymbol = stake.TokenSymbol,
                TokenName = stake.TokenName,
                TokenAmount = stake.TokenAmount,
                TokenPrice = stake.TokenPrice,
                MonthDuration = stake.MonthDuration,
                StartMoment = stake.StartMoment,
                EndMoment = stake.EndMoment,
                TotalProfitWithdrawn = stake.TotalProfitWithdrawn,
                TotalAmountWithdrawn = stake.TotalAmountWithdrawn,
                State = stake.State,
                EachMonthProfit = stake.EachMonthProfit,
                EachMonthProfitPercent = stake.EachMonthProfitPercent,
                CreatedMoment = stake.CreatedMoment,
                ModifiedMoment = stake.ModifiedMoment,
            };

        }


        /// <summary>
        /// use for check token existing and return token data
        /// </summary>
        /// <param name="tokenName"></param>
        /// <returns></returns>
        /// <exception cref="BadRequestException"></exception>
        private AvailableTokenData ValidateToken(string tokenName)
        {
            var tokenData = _availableTokenData.FirstOrDefault(q => q.Name.Equals(tokenName, StringComparison.CurrentCultureIgnoreCase))
                ?? throw new BadRequestException($"Unsupported token name! {tokenName}");
            return tokenData;
        }


        private static string GenerateBytes32HexId()
        {
            var buffer = new byte[32];
            RandomNumberGenerator.Fill(buffer);

            var newId = BitConverter.ToString(buffer)
                .Replace("-", "")
                .ToLowerInvariant();

            return newId;
        }

        
    }
}
