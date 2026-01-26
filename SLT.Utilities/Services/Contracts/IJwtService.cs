using Microsoft.AspNetCore.Mvc;
using Utilities.Enums;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Utilities.Services.Contracts
{
    public interface IJwtService
    {
        AccessToken Generate(IEnumerable<Claim> claims);
        JwtSecurityToken Validate(string token);
        ActionResult Authenticate(string publicKey, IEnumerable<string> permissions,
            UserType userType, string securityStamp);

    }
}