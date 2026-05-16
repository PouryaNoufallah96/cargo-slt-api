using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using SLT.Services._Stake;
using SLT.Services._Stake.DTOs.Results;
using SLT.Services._Stake.DTOs.Updates;
using Swashbuckle.AspNetCore.Annotations;
using Utilities.Api;
using Utilities.Attributes;
using Utilities.Filters;

namespace SLT.Api.Controllers.V1
{
    [ApiController]
    [ApiResultFilter]
    [ApiVersion("1")]
    [Route("api/v{version:apiVersion}/[controller]")]
    public class StakeController(IStakeService _stakeService) : ApiBaseController
    {
        [HttpPost("[action]")]
        [CustomRateLimit]
        [Authorize(RequireActiveUser = true)]
        [SwaggerOperation(Summary = "Create Pending Stake ", Tags = ["Stake"])]
        public async Task<StakeResult> CreateStakeAsync(CreateStakeUpdate update)
        {
            return await _stakeService.CreateStakeAsync(
                update,
                WalletAddress,
                NetworkType
            );
        }


        [HttpPost("[action]")]
        [CustomRateLimit]
        [Authorize(RequireActiveUser = false)]
        [SwaggerOperation(Summary = "Get stake history", Tags = ["Stake"])]
        public async Task<StakeListResult> GetStakeHistoryAsync(StakeHistoryUpdate update)
        { 
            return await _stakeService.GetStakeHistoryAsync(
                update,
                WalletAddress
            );
        }

        [HttpPost("[action]")]
        [CustomRateLimit]
        [Authorize(RequireActiveUser = false)]
        [SwaggerOperation(Summary = "Get one stake Detail", Tags = ["Stake"])]
        public async Task<StakeDetailResult> GetStakeDetailAsync(StakeDetailUpdate update)
        { 
            return await _stakeService.GetStakeDetailAsync(
                update,
                WalletAddress
            );
        }

        [HttpPost("[action]")]
        [CustomRateLimit]
        [Authorize(RequireActiveUser = false)]
        [SwaggerOperation(Summary = "Get Wallet stats for stake side", Tags = ["Stake"])]
        public async Task<List<StakeWalletStatsResult>> GetWalletStatsAsync(GetStakeWalletStatsUpdate update)
        { 
            return await _stakeService.GetWalletStatsAsync(
                update,
                WalletAddress
            );
        }


    }
}
