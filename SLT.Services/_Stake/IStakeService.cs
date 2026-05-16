using SLT.Services._Stake.DTOs.Results;
using SLT.Services._Stake.DTOs.Updates;

namespace SLT.Services._Stake
{
    public interface IStakeService
    {
        Task<StakeResult> CreateStakeAsync(CreateStakeUpdate update, string walletAddress, string network);
        Task<StakeListResult> GetStakeHistoryAsync(StakeHistoryUpdate update, string evmWalletAddress);
        Task<StakeDetailResult> GetStakeDetailAsync(StakeDetailUpdate update, string evmWalletAddress);
        Task<List<StakeWalletStatsResult>> GetWalletStatsAsync(GetStakeWalletStatsUpdate update, string evmWalletAddress);
        Task RemoveNotRegisteredStakesAsync();
        Task ActivateStakeAsync(string depositRef, string hash);


    }
}
 