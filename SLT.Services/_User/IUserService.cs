using Microsoft.AspNetCore.Mvc;
using SLT.Services._User.DTOs.Results;
using SLT.Services._User.DTOs.Updates;

namespace SLT.Services._User
{
    public interface IUserService
    {

        //auth 
        NonceResult GetNonce(NonceRequest update, string ip);
        Task<ActionResult> GetToken(NonceVerification update, string ip);
        Task<GetUserResult> GetUserAsync(string whois);
        Task<ActionResult> GetTokenWithPureWalletAddress(GetTokenWithPureWalletAddress update, string ip);
        Task<GetUserStatsResult> GetUserStatsAsync(string whois, string walletAddress);

    }
}
 