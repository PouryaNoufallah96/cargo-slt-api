using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;

using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Utilities.Constants;
using Utilities.Enums;
using Utilities.Exceptions.Common;
using Utilities.Services.Contracts;
using Utilities.Utilities;
using static Utilities.Constants.RegisterMode;

namespace Utilities.Services
{
    public class JwtService(JwtServiceSettings _settings) : IJwtService, ISingletonDependency
    {
        public AccessToken Generate(IEnumerable<Claim> claims)
        {
            var signatureKey = Encoding.UTF8.GetBytes(_settings.SignatureKey); // longer than 16 character
            var signingCredentials = new SigningCredentials(new SymmetricSecurityKey(signatureKey),
                SecurityAlgorithms.HmacSha256Signature);

            var encryptionKey = Encoding.UTF8.GetBytes(_settings.EncryptionKey); //must be 16 character
            var encryptingCredentials = new EncryptingCredentials(new SymmetricSecurityKey(encryptionKey),
                SecurityAlgorithms.Aes128KW, SecurityAlgorithms.Aes128CbcHmacSha256);

            var descriptor = new SecurityTokenDescriptor
            {
                Issuer = _settings.Issuer,
                Audience = _settings.Audience,
                IssuedAt = DateTime.UtcNow,
                NotBefore = DateTime.UtcNow,
                Expires = DateTime.UtcNow.AddMinutes(_settings.ExpiresAfter),
                SigningCredentials = signingCredentials,
                EncryptingCredentials = encryptingCredentials,
                Subject = new ClaimsIdentity(claims),
            };

            var tokenHandler = new JwtSecurityTokenHandler();

            var securityToken = tokenHandler.CreateJwtSecurityToken(descriptor);


            return new AccessToken(securityToken);
        }

        public JwtSecurityToken Validate(string token)
        {
            var tokenHandler = new JwtSecurityTokenHandler();

            var signatureKey = Encoding.UTF8.GetBytes(_settings.SignatureKey); // longer that 16 character
            var signingCredentials = new SigningCredentials(new SymmetricSecurityKey(signatureKey),
                SecurityAlgorithms.HmacSha256Signature);

            var encryptionKey = Encoding.UTF8.GetBytes(_settings.EncryptionKey); //must be 16 character
            var encryptingCredentials = new EncryptingCredentials(new SymmetricSecurityKey(encryptionKey),
                SecurityAlgorithms.Aes128KW, SecurityAlgorithms.Aes128CbcHmacSha256);

            tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = signingCredentials.Key,
                TokenDecryptionKey = encryptingCredentials.Key,
                ValidateIssuer = true,
                ValidIssuer = _settings.Issuer,
                ValidateAudience = true,
                ValidAudience = _settings.Audience,
                ClockSkew = TimeSpan.Zero,

            }, out SecurityToken validatedToken);

            return (JwtSecurityToken)validatedToken;
        }

        public ActionResult Authenticate(string publicKey, IEnumerable<string> permissions,
            UserType userType, string securityStamp)
            => new JsonResult(Generate(GetClaimsAsync(publicKey, permissions, userType, securityStamp)));

        #region Private Methods

        private static List<Claim> GetClaimsAsync(string publicKey, IEnumerable<string> permissions,
            UserType userType, string securityStamp)
        {
            try
            {
                var claims = new List<Claim>
                {
                    new(Claims.PublicKey.ToDisplay(), publicKey),
                    new(Claims.UserType.ToDisplay(),userType.ToDisplay()),
                    new(Claims.SecurityStamp.ToDisplay(), securityStamp)
                };

                claims.AddRange(permissions.Select(permission =>
                    new Claim(Claims.Permission.ToDisplay(), permission)));

                return claims;
            }
            catch (Exception ex)
            {
                throw new BaseException(ex.Message);
            }
        }

        #endregion
    }

    public class AccessToken
    {
        public string access_token { get; set; }
        //public string refresh_token { get; set; }
        public string token_type { get; set; }
        public int expires_in { get; set; }

        public AccessToken(JwtSecurityToken securityToken)
        {
            access_token = new JwtSecurityTokenHandler().WriteToken(securityToken);
            token_type = "Bearer";
            expires_in = (int)(securityToken.ValidTo - DateTime.UtcNow).TotalSeconds;
        }
    }

    public class TokenRequest
    {
        [Required]
        public string grant_type { get; set; }
        [Required]
        public string username { get; set; }
        [Required]
        public string password { get; set; }
        public string refresh_token { get; set; }
        public string scope { get; set; }
        public string client_id { get; set; }
        public string client_secret { get; set; }
    }
}