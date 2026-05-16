using Microsoft.AspNetCore.SignalR;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using SLT.Domain.Collections;
using SLT.Domain.Repositories;
using SLT.Domain.Repositories.Contracts;
using SLT.Services._BlockChain;
using SLT.Services._Price.DTOs.Settings;
using SLT.Services._Stake.DTOs.Settings;
using SLT.Services._TransactionLog._Hub;
using SLT.Services._Withdrawal.DTOs.Results;
using SLT.Services._Withdrawal.DTOs.Updates;
using System.Numerics;
using Utilities.Exceptions.Common;
using static Utilities.Constants.RegisterMode;

namespace SLT.Services._Withdrawal
{

    public class WithdrawalService(
        IWithdrawalRepository _withdrawalRepository,
        IBlockChainService _blockChainService,
        StakeSetting _stakeSetting,
        IHubContext<WalletNotifyHub> _hubContext,
        IStakeRepository _stakeRepository,
        AvailableTokensSettings _availableTokenData) : IWithdrawalService, IScopedDependency
    {

        public async Task SyncStakeWithdrawalsAsync(string stakeReference, bool syncProfit, bool syncAmount)
        {
            var stake = await _stakeRepository.AsQueryable()
                .FirstOrDefaultAsync(x => x.StakeReference == stakeReference)
                ?? throw new NotFoundException("Stake not found!");

            var withdrawals = _withdrawalRepository.AsQueryable()
                .Where(w =>
                    w.StakeReference == stakeReference &&
                    w.State == WithdrawalState.Success);

            if (syncProfit)
            {
                stake.TotalProfitWithdrawn = await withdrawals
                    .Where(w => w.Type == WithdrawalType.StakeProfit)
                    .SumAsync(w => (decimal?)w.ProfitAmount) ?? 0m;
            }

            if (syncAmount)
            {
                var stakeWithdrawals = withdrawals
                    .Where(w => w.Type == WithdrawalType.StakeWithdrawal);

                stake.TotalAmountWithdrawn = await stakeWithdrawals
                    .SumAsync(w => (decimal?)w.Amount) ?? 0m;

                stake.TotalCostOfAmountWithdrawn = await stakeWithdrawals
                    .SumAsync(w => (decimal?)w.Cost) ?? 0m;

                stake.TotalProfitOfAmountWithdrawn = await stakeWithdrawals
                    .SumAsync(w => (decimal?)w.ProfitAmount) ?? 0m;
            }

            var totalOut = stake.TotalAmountWithdrawn;
                //stake.TotalAmountWithdrawn +
                //stake.TotalProfitOfAmountWithdrawn -
                //stake.TotalCostOfAmountWithdrawn;
            if (totalOut >= stake.StartAmount && stake.State != StakeState.Finished)
            {
                stake.State = StakeState.Finished;
            }

            await _stakeRepository.ReplaceOneAsync(stake);
        }

        public async Task CreateProfitWithdrawaByEventAsycn(string depositRef, BigInteger amount, string hash)
        {
            var stake = await _stakeRepository.AsQueryable()
           .FirstOrDefaultAsync(q => q.StakeReference == depositRef);

            if (stake == null)
                return;
            var now = DateTime.UtcNow;
            var tokenData = ValidateToken(stake.TokenName);

            var profitAmount = _blockChainService.ConvertFromWei(
                amount,
                tokenData.PriceDecimalPlaces);

            var withdrawal = new Withdrawal
            {
                WithdrawalRerefence = Guid.NewGuid().ToString("N"),
                StakeReference = stake.StakeReference,
                WalletAddress = stake.WalletAddress,
                Symbol = stake.TokenSymbol,
                Network = stake.TokenNetworkName,
                Amount = 0,
                Cost = 0m,
                ProfitAmount = profitAmount,
                FinalAmount = profitAmount,
                Type = WithdrawalType.StakeProfit,
                State = WithdrawalState.Success,
                RegisterMoment = now,
                Hash = hash,
            };
            await _withdrawalRepository.InsertOneAsync(withdrawal);
            await SyncStakeWithdrawalsAsync(stake.StakeReference, true, false);

            var shortHash = hash[..10];

            await _hubContext.Clients
              .Group(stake.WalletAddress)
              .SendAsync(
                  "PaymentMessage",
                  $"Stake profit received successfully  Amount: {profitAmount:N4} {stake.TokenSymbol} , Tx: {shortHash}..."
              );

        }

        public async Task CreateEarlyWithdrawnByEventAsync(string depositRef, string hash,
            BigInteger withdrawAmount, BigInteger profitAmount, BigInteger costAmont)
        {
            var stake = await _stakeRepository.AsQueryable()
          .FirstOrDefaultAsync(q => q.StakeReference == depositRef);

            if (stake == null)
                return;
            var now = DateTime.UtcNow;
            var tokenData = ValidateToken(stake.TokenName);

            var withdraw = _blockChainService.ConvertFromWei(
                withdrawAmount,
                tokenData.PriceDecimalPlaces);

            var profit = _blockChainService.ConvertFromWei(
                profitAmount,
                tokenData.PriceDecimalPlaces);

            var cost = _blockChainService.ConvertFromWei(
                costAmont,
                tokenData.PriceDecimalPlaces);

            var finalAmount = (withdraw + profit) - cost;

            var withdrawal = new Withdrawal
            {
                WithdrawalRerefence = Guid.NewGuid().ToString("N"),
                StakeReference = stake.StakeReference,
                WalletAddress = stake.WalletAddress,
                Symbol = stake.TokenSymbol,
                Network = stake.TokenNetworkName,
                Amount = withdraw,
                Cost = cost,
                ProfitAmount = profit,
                FinalAmount = finalAmount,
                Type = WithdrawalType.StakeWithdrawal,
                State = WithdrawalState.Success,
                RegisterMoment = now,
                Hash = hash,
            };

            await _withdrawalRepository.InsertOneAsync(withdrawal);
            await SyncStakeWithdrawalsAsync(stake.StakeReference, false, true);

            var shortHash = hash[..10];

            await _hubContext.Clients
            .Group(stake.WalletAddress)
            .SendAsync(
                "PaymentMessage",
                $"Your staking withdrawal was processed successfully. You received {finalAmount:N4} {stake.TokenSymbol}."
            );
        }

        public async Task CreateWithdrawnAllByEventAsync(string depositRef, string hash, BigInteger withdrawAmount, BigInteger profitAmount)
        {
            var stake = await _stakeRepository.AsQueryable()
            .FirstOrDefaultAsync(q => q.StakeReference == depositRef);

            if (stake == null)
                return;
            var now = DateTime.UtcNow;
            var tokenData = ValidateToken(stake.TokenName);

            var withdraw = _blockChainService.ConvertFromWei(
                withdrawAmount,
                tokenData.PriceDecimalPlaces);

            var profit = _blockChainService.ConvertFromWei(
                profitAmount,
                tokenData.PriceDecimalPlaces);

            var finalAmount = (withdraw + profit);

            var withdrawal = new Withdrawal
            {
                WithdrawalRerefence = Guid.NewGuid().ToString("N"),
                StakeReference = stake.StakeReference,
                WalletAddress = stake.WalletAddress,
                Symbol = stake.TokenSymbol,
                Network = stake.TokenNetworkName,
                Amount = withdraw,
                Cost = 0,
                ProfitAmount = profit,
                FinalAmount = finalAmount,
                Type = WithdrawalType.StakeWithdrawal,
                State = WithdrawalState.Success,
                RegisterMoment = now,
                Hash = hash,
            };

            await _withdrawalRepository.InsertOneAsync(withdrawal);
            await SyncStakeWithdrawalsAsync(stake.StakeReference, false, true);

            var shortHash = hash[..10];

            await _hubContext.Clients
            .Group(stake.WalletAddress)
            .SendAsync(
                "PaymentMessage",
                $"Your staking withdrawal was processed successfully. You received {finalAmount:N4} {stake.TokenSymbol}."
            );
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


    }
}


//public async Task<WithdrawalResult> WithdrawStakeProfitAsync(WithdrawStakeProfitUpdate update, string publicKey, string evmWalletAddress)
//{

//    var stake = await _stakeRepository.AsQueryable()
//        .FirstOrDefaultAsync(q => q.StakeReference == update.StakeReference)
//        ?? throw new NotFoundException("Stake not found!");

//    if (string.IsNullOrWhiteSpace(publicKey) || publicKey == "guess")
//        throw new BadRequestException("Access denied");

//    if (stake.UserPublicKey != publicKey)
//        throw new BadRequestException("Access denied");

//    if (stake.State != StakeState.Active)
//        throw new BadRequestException("Stake is not active");

//    var now = DateTime.UtcNow;
//    var tokenData = ValidateToken(stake.TokenName);


//    var lastStakeWithdrawal = await _withdrawalRepository.AsQueryable()
//        .Where(w =>
//            w.StakeReference == stake.StakeReference &&
//            w.Type == WithdrawalType.StakeWithdrawal &&
//            w.State == WithdrawalState.Success)
//        .OrderByDescending(w => w.RegisterMoment)
//        .FirstOrDefaultAsync();

//    var profitStartDate = lastStakeWithdrawal?.RegisterMoment ?? stake.StartMoment;


//    var passedMonths = GetPassedFullMonths(profitStartDate, now);

//    if (passedMonths <= 0)
//        throw new BadRequestException("No profit available yet");


//    var monthlyPercent = stake.EachMonthProfitPercent / 100m;

//    var monthlyProfit = stake.TokenAmount * monthlyPercent;

//    var totalProfit = monthlyProfit * passedMonths;


//    var finalAmountInWei = _blockChainService.ConvertToWei(
//        totalProfit,
//        tokenData.PriceDecimalPlaces);

//    var withdrawal = new Withdrawal
//    {
//        WithdrawalRerefence = Guid.NewGuid().ToString("N"),
//        StakeReference = stake.StakeReference,
//        UserPublicKey = stake.UserPublicKey,
//        WalletAddress = stake.WalletAddress,
//        Symbol = stake.TokenSymbol,
//        Network = stake.TokenNetworkName,

//        Amount = 0,
//        Cost = 0m,
//        ProfitAmount = totalProfit,
//        FinalAmount = totalProfit,
//        FinalAmountInWei = finalAmountInWei.ToString(),
//        Type = WithdrawalType.StakeProfit,
//        State = WithdrawalState.NotRegistered,

//        RegisterMoment = null,
//        RegisterHash = null,
//        Hash = null,
//    };
//    await _withdrawalRepository.InsertOneAsync(withdrawal);
//    await SyncStakeWithdrawalsAsync(stake.StakeReference, true, false);
//    return ConvertToResult(withdrawal);
//}
//public async Task<WithdrawalResult> WithdrawStakeAmountAsync(WithdrawStakeAmountUpdate update, string publicKey, string evmWalletAddress)
//{

//    var stake = await _stakeRepository.AsQueryable()
//        .FirstOrDefaultAsync(q => q.StakeReference == update.StakeReference)
//        ?? throw new NotFoundException("Stake not found!");

//    if (string.IsNullOrWhiteSpace(publicKey) || publicKey == "guess")
//        throw new BadRequestException("Access denied");

//    if (stake.UserPublicKey != publicKey)
//        throw new BadRequestException("Access denied");

//    if (stake.State != StakeState.Active)
//        throw new BadRequestException("Stake is not active");

//    var now = DateTime.UtcNow;

//    var tokenData = ValidateToken(stake.TokenName);

//    var totalWithdrawnAmount = await _withdrawalRepository.AsQueryable()
//        .Where(w =>
//            w.StakeReference == stake.StakeReference &&
//            w.Type == WithdrawalType.StakeWithdrawal &&
//            w.State == WithdrawalState.Success)
//        .SumAsync(w => (decimal?)w.Amount) ?? 0m;

//    var remainingAmount = stake.StartAmount - totalWithdrawnAmount;

//    if (remainingAmount <= 0)
//        throw new BadRequestException("No balance left in stake");

//    if (update.TokenAmount > remainingAmount)
//        throw new BadRequestException("Requested amount exceeds available balance");

//    //  reset point
//    var lastStakeWithdrawal = await _withdrawalRepository.AsQueryable()
//        .Where(w =>
//            w.StakeReference == stake.StakeReference &&
//            w.Type == WithdrawalType.StakeWithdrawal &&
//            w.State == WithdrawalState.Success)
//        .OrderByDescending(w => w.RegisterMoment)
//        .FirstOrDefaultAsync();

//    var profitStartDate = lastStakeWithdrawal?.RegisterMoment ?? stake.StartMoment;


//    var passedMonths = GetPassedFullMonths(profitStartDate, now);

//    //  ProfitAmount ( RULE)
//    decimal profitAmount = 0;

//    if (passedMonths > 0)
//    {
//        var rule = GetEarlyWithdrawPercentPerMonth(passedMonths);

//        profitAmount = stake.TokenAmount * rule * passedMonths; // not update.tokenAmount
//    }

//    //  Cost 
//    var cost = await _withdrawalRepository.AsQueryable()
//        .Where(w =>
//            w.StakeReference == stake.StakeReference &&
//            w.Type == WithdrawalType.StakeProfit &&
//            w.State == WithdrawalState.Success &&
//            w.RegisterMoment > profitStartDate)
//        .SumAsync(w => (decimal?)w.ProfitAmount) ?? 0m;

//    //  Stake
//    stake.TokenAmount -= update.TokenAmount;
//    stake.TotalAmountWithdrawn += update.TokenAmount;

//    stake.EachMonthProfit = stake.TokenAmount * (stake.EachMonthProfitPercent / 100);

//    if (stake.TokenAmount <= 0)
//        stake.State = StakeState.Withdraw;

//    var finalAmount = (update.TokenAmount + profitAmount) - cost;

//    var finalAmountInWei = _blockChainService.ConvertToWei(finalAmount, tokenData.PriceDecimalPlaces);

//    var withdrawal = new Withdrawal
//    {
//        WithdrawalRerefence = Guid.NewGuid().ToString("N"),
//        StakeReference = stake.StakeReference,
//        UserPublicKey = stake.UserPublicKey,
//        WalletAddress = stake.WalletAddress,
//        Symbol = stake.TokenSymbol,
//        Network = stake.TokenNetworkName,

//        Amount = update.TokenAmount,
//        ProfitAmount = profitAmount,
//        Cost = cost,

//        State = WithdrawalState.NotRegistered,
//        FinalAmountInWei = finalAmountInWei.ToString(),
//        FinalAmount = finalAmount,
//        Type = WithdrawalType.StakeWithdrawal,
//        RegisterMoment = null,
//        RegisterHash = null,
//        Hash = null
//    };

//    await _withdrawalRepository.InsertOneAsync(withdrawal);
//    await _stakeRepository.ReplaceOneAsync(stake);
//    return ConvertToResult(withdrawal);
//}


//private int GetPassedFullMonths(DateTime start, DateTime now)
//{
//    int months = (now.Year - start.Year) * 12 + (now.Month - start.Month);

//    if (now.Day < start.Day)
//        months--;

//    return Math.Max(0, months);
//}

//private decimal GetEarlyWithdrawPercentPerMonth(int passedMonths)
//{
//    var rule = _stakeSetting.EarlyWithdrawRules
//        .FirstOrDefault(r => passedMonths >= r.FromMonth && passedMonths < r.ToMonth);

//    if (rule == null)
//        throw new BadRequestException("Early withdraw rule not found");

//    return rule.MonthlyProfitPercent / 100m;
//}


//private WithdrawalResult ConvertToResult(Withdrawal w)
//{
//    return new WithdrawalResult
//    {
//        Amount = w.Amount,
//        Cost = w.Cost,
//        FinalAmountInWei = w.FinalAmountInWei,
//        ProfitAmount = w.ProfitAmount,
//        FinalAmount = w.FinalAmount,
//        CreatedMoment = w.CreatedMoment,
//        ModifiedMoment = w.ModifiedMoment,
//        Network = w.Network,
//        State = w.State,
//        Symbol = w.Symbol,
//        Type = w.Type,
//        UserPublicKey = w.UserPublicKey,
//        WalletAddress = w.WalletAddress,
//        WithdrawalRerefence = w.WithdrawalRerefence
//    };
//}