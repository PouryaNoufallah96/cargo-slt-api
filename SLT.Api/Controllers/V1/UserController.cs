using Asp.Versioning;
using Utilities.Api;
using Utilities.Attributes;
using Utilities.Filters;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using SLT.Services._User;
using SLT.Services._User.DTOs.Storages;
using SLT.Services._User.DTOs.Updates;
using SLT.Services._User.DTOs.Results;
using SLT.Services._BlockChain;

namespace CoinHalls.Api.Controllers.V1
{
    [ApiController]
    [ApiResultFilter]
    [ApiVersion("1")]
    [Route("api/v{version:apiVersion}/[controller]")]
    public class UserController(IUserService _userService, JwtBlacklistStorage _blacklist) : ApiBaseController
    {


        [HttpPost("[action]")] 
        [CustomRateLimit(maxAttemptsCount: 50)]
        [SwaggerOperation(Summary = "For getting Nonce", Tags = ["Auth"])]
        public NonceResult GetNonce(NonceRequest update )
        {
            return _userService.GetNonce(update, Ip);
        }



        [HttpPost("[action]")]
        [CustomRateLimit(maxAttemptsCount: 50)]
        [SwaggerOperation(Summary = "For getting JWT token", Tags = ["Auth"])]
        public async Task<ActionResult> GetToken(NonceVerification update)
        {
            return await _userService.GetToken(update, Ip);
        }


        [HttpPost("[action]")]
        [Authorize]
        [CustomRateLimit(maxAttemptsCount: 50)]
        [SwaggerOperation(Summary = "For getting User data", Tags = ["Auth"])]
        public async Task<GetUserResult> GetUserAsync()
        {
            return await _userService.GetUserAsync(PublicKey);
        }

        [HttpPost("[action]")]
        [CustomRateLimit(maxAttemptsCount: 45)]
        [SwaggerOperation(Summary = "For getting JWT token with pure wallet address ", Tags = ["Auth"])]
        public async Task<ActionResult> GetTokenWithPureWalletAddress(GetTokenWithPureWalletAddress update)
        {
            return await _userService.GetTokenWithPureWalletAddress(update, Ip);
        }


        [HttpPost("logout")]
        [CustomRateLimit(maxAttemptsCount: 50)]
        [SwaggerOperation(Summary = "For logout user", Tags = ["Auth"])]
        [Authorize(RequireActiveUser = false)]
        public IActionResult Logout()
        {
            var token = Request.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last();
            if (string.IsNullOrEmpty(token))
                return BadRequest("No token provided");

            var expiry = DateTime.UtcNow.AddHours(1);

            _blacklist.AddToken(token, expiry);

            return Ok("Logged out successfully");
        }



        [HttpGet("[action]")]
        [CustomRateLimit(maxAttemptsCount: 60)]
        [SwaggerOperation(Summary = "System Status", Tags = ["Status"])]
        public async Task<SystemStatus> GetStatus()
        {
            return new SystemStatus { };
        }

        [HttpGet("[action]")]
        [Authorize(RequireActiveUser = false)]
        [CustomRateLimit(maxAttemptsCount: 50)]
        [SwaggerOperation(Summary = "For getting user available stages stats", Tags = ["Stats"])]
        public async Task<GetUserStatsResult> GetUserStatsAsync()
        {
            return await _userService.GetUserStatsAsync(PublicKey, WalletAddress);
        }

    }
    public class SystemStatus
    {
        public bool Checked { get; set; } = false;
        public bool SystemHealth { get; set; } = true;
        public bool SystemActivity { get; set; } = true;
        public string ActivityMessage { get; set; } = "All services is active.";
        public string Message { get; set; } = "Use the SLT web application to apply changes or complete transactions.";
        //public string Message { get; set; } = null;

        //To apply changes or complete transactions, please connect your wallet via WalletConnect in Settings.
    }
}
