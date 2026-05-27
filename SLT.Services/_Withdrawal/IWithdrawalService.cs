using SLT.Services._Withdrawal.DTOs.Results;
using SLT.Services._Withdrawal.DTOs.Updates;
using System.Numerics;

namespace SLT.Services._Withdrawal
{
    public interface IWithdrawalService
    {
        Task SyncStakeWithdrawalsAsync(string stakeReference, bool syncProfit, bool syncAmount);
        Task CreateProfitWithdrawaByEventAsycn(string depositRef, BigInteger amount, string hash);
        //Task CreateEarlyWithdrawnByEventAsync(string depositRef, string hash, BigInteger withdrawAmount, BigInteger profitAmount, BigInteger costAmont);
        Task CreateWithdrawnAllByEventAsync(string depositRef, string hash, BigInteger withdrawAmount, BigInteger profitAmount);

        //Task<WithdrawalResult> WithdrawStakeProfitAsync(WithdrawStakeProfitUpdate update, string publicKey, string evmWalletAddress);
        //Task<WithdrawalResult> WithdrawStakeAmountAsync(WithdrawStakeAmountUpdate update, string publicKey, string evmWalletAddress);
    }
}
