using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using Nethereum.Signer;
using Nethereum.Util;
using SLT.Domain.Collections;
using SLT.Domain.Repositories.Contracts;
using SLT.Services._User.DTOs.Results;
using SLT.Services._User.DTOs.Settings;
using SLT.Services._User.DTOs.Storages;
using SLT.Services._User.DTOs.Updates;
using System.Security.Claims;
using Utilities.Constants;
using Utilities.Enums;
using Utilities.Exceptions;
using Utilities.Exceptions.Common;
using Utilities.Services.Contracts;
using Utilities.Utilities;
using static Utilities.Constants.RegisterMode;

namespace SLT.Services._User
{
    public class UserService(
    IRandomService _randomService,
    JwtServiceSettings _jwtSettings,
    IJwtService _jwtService,
    ILogger<UserService> _logger,
    IUserRepository _userRepository,
    UserAuthStorage _userAuthStorage) : IUserService, IScopedDependency
    {

        /// <summary>
        /// used for create one time nonce
        /// </summary>
        /// <param name="update"></param>
        /// <param name="ip"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public NonceResult GetNonce(NonceRequest update, string ip)
        {
            ValidateClientInfo(update.ClientId, update.ClientSecret);
            var walletAddress = ValidateAndConvertToChecksumAddress(update.WalletAddress);
            var random = _randomService.GetSecureAlphaNumericString(6);
            var newNonce = random + Guid.NewGuid().ToString("N");
            var newUserAuthData = new UserAuthData
            {
                Nonce = newNonce,
                WalletAddress = update.WalletAddress,
                GeneratedMoment = DateTime.UtcNow,
                NetworkType = update.NetworkType,
                IP = ip,
            };

            _userAuthStorage.AddItem(newNonce, newUserAuthData);

            return new NonceResult
            {
                ExpireMoment = newUserAuthData.GeneratedMoment.AddMinutes(2),
                Nonce = newNonce,
                Message = $"Please sign this message to authenticate with SLT: {newNonce}"
            };
        }


        /// <summary>
        /// this method is for get jwt token
        /// here check the nonce and wallet and signature with  nethereium
        /// throw error if data is not valid
        /// generate jwt token for valid data for login and update nonce storage
        /// </summary>
        /// <param name="update"></param>
        /// <param name="ip"></param>
        /// <returns></returns>
        public async Task<ActionResult> GetToken(NonceVerification update, string ip)
        {
            ValidateClientInfo(update.ClientId, update.ClientSecret);

            var userAuthData = ValidateNonce(update.Nonce, update.WalletAddress, update.NetworkType);

            var message = $"Please sign this message to authenticate with SLT: {update.Nonce}";
            VerifySignature(message, update.Signature, userAuthData.WalletAddress);

            var user = await GetOrCreateUserAsync(userAuthData.WalletAddress);

            _userAuthStorage.RemoveItem(update.Nonce);

            return Authenticate(user,update.NetworkType);
        }


        /// <summary>
        /// for get user data 
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        /// <exception cref="NotFoundException"></exception>
        public async Task<GetUserResult> GetUserAsync(string whois)
        {
            var user = await _userRepository.AsQueryable()
                .Where(u => u.UserPublicKey == whois)
                .FirstOrDefaultAsync();

            return user == null
                ? throw new NotFoundException("User Not Found!")
                : new GetUserResult
                {
                    CreateMoment = user.CreatedMoment,
                    WalletAddress = user.WalletAddress,
                    LoginHistories = user.LoginDates
                };
        }



        /// <summary>
        /// this method is for get jwt token
        /// here check the nonce and wallet and signature with  nethereium
        /// throw error if data is not valid
        /// generate jwt token for valid data for login and update nonce storage
        /// </summary>
        /// <param name="update"></param>
        /// <param name="ip"></param>
        /// <returns></returns>
        public async Task<ActionResult> GetTokenWithPureWalletAddress(GetTokenWithPureWalletAddress update, string ip)
        {
            ValidateClientInfo(update.ClientId, update.ClientSecret);
            var walletAddress = ValidateAndConvertToChecksumAddress(update.WalletAddress);

            var user = new User
            {
                WalletAddress = update.WalletAddress,
                Status = UserStatus.NotVerified,
                SecurityStamp = "-",
                Role = UserRole.Customer,
                Id = "guess"
            };

            return Authenticate(user,NetworkType.BEP20);
        }

        /// <summary>
        /// this method use for get user stats in each available stage
        /// </summary>
        /// <param name="userPublicKey"></param>
        /// <param name="walletAddress"></param>
        /// <returns></returns>
        public async Task<GetUserStatsResult> GetUserStatsAsync(string whois, string walletAddress)
        {

            var result = new List<GetUserStatsResult>();

            if (whois == "guess") return new GetUserStatsResult
            {
                UserStatus = UserStatus.NotVerified,
            };

            var user = await _userRepository.AsQueryable()
                .Where(q => q.WalletAddress.ToLower() == walletAddress.ToLower())
                .FirstOrDefaultAsync();

            return new GetUserStatsResult
            {
                UserStatus = UserStatus.Active,
            };
        }


        #region Private Methods
        /// <summary>
        /// Validates the provided nonce and associated wallet address.
        /// </summary>
        /// <param name="Nonce">The unique nonce value issued to the user for authentication.</param>
        /// <param name="walletAddress">The wallet address provided by the client.</param>
        /// <returns>The <see cref="UserAuthData"/> associated with the nonce if validation succeeds.</returns>
        /// <exception cref="NonceNotFoundException">Thrown if the nonce does not exist in the storage.</exception>
        /// <exception cref="BadRequestException">
        /// Thrown if the nonce has expired or if the provided wallet address does not match the one stored for this nonce.
        /// </exception>
        /// <remarks>
        /// This method ensures:
        /// 1. The nonce exists in the <see cref="UserAuthStorage"/>.
        /// 2. The nonce has not expired (valid for 10 seconds from creation).
        /// 3. The provided wallet address matches the one originally associated with the nonce.
        /// </remarks>
        private UserAuthData ValidateNonce(string Nonce, string walletAddress, NetworkType networkType)
        {
            var userAuthData = _userAuthStorage.GetItem(Nonce) ?? throw new NonceNotFoundException();

            if (DateTime.UtcNow - userAuthData.GeneratedMoment > TimeSpan.FromSeconds(300))
            {
                _userAuthStorage.RemoveItem(Nonce);
                throw new BadRequestException("nonce expired!");
            }

            if (!string.Equals(userAuthData.WalletAddress, walletAddress, StringComparison.OrdinalIgnoreCase))
                throw new BadRequestException("Wallet address mismatch for nonce");

            if (userAuthData.NetworkType != networkType) throw new BadRequestException("network mismatch for nonce");

            return userAuthData;
        }


        /// <summary>
        /// Verifies that the provided cryptographic signature matches the given wallet address for the specified message.
        /// </summary>
        /// <param name="message">The original message that was signed by the wallet.</param>
        /// <param name="signatureHex">The hexadecimal signature generated by the wallet for the message.</param>
        /// <param name="walletAddress">The expected wallet address that allegedly signed the message.</param>
        /// <exception cref="BadRequestException">
        /// Thrown if:
        /// <list type="bullet">
        /// <item>The message, signature, or wallet address is null, empty, or whitespace.</item>
        /// <item>The recovered address from the signature does not match the provided wallet address.</item>
        /// </list>
        /// </exception>
        /// <exception cref="BaseException">
        /// Thrown if there is an error during the Web3/Ethereum signature recovery process.
        /// </exception>
        /// <remarks>
        /// This method uses <see cref="EthereumMessageSigner"/> from the Nethereum library to recover the address
        /// from the provided message and signature. It ensures that the signature is valid for the given wallet
        /// address according to the Ethereum (EVM) signing standard.
        /// </remarks>
        private void VerifySignature(string message, string signatureHex, string walletAddress)
        {
            if (string.IsNullOrWhiteSpace(message) ||
                string.IsNullOrWhiteSpace(signatureHex) ||
                string.IsNullOrWhiteSpace(walletAddress))
                throw new BadRequestException("invalid input!");

            try
            {
                var signer = new EthereumMessageSigner();

                var recovered = signer.EncodeUTF8AndEcRecover(message, signatureHex);

                var isVerified = string.Equals(recovered, walletAddress, StringComparison.OrdinalIgnoreCase);
                if (!isVerified) throw new BadRequestException("Invalid signature for wallet");
            }
            catch
            {
                throw new BaseException("error in web3 network!");
            }
        }


        /// <summary>
        /// for validate the ClientInformation , OAuth2 verification
        /// </summary>
        /// <param name="clientId"></param>
        /// <param name="clientSecret"></param>
        /// <exception cref="BadRequestException"></exception>
        private void ValidateClientInfo(string clientId, string clientSecret)
        {
            if (!clientId.HasValue() ||
                !clientSecret.HasValue() ||
                !_jwtSettings.ClientInfo.ContainsKey(clientId.ToLower()) ||
                !_jwtSettings.ClientInfo[clientId.ToLower()].Equals(clientSecret, StringComparison.OrdinalIgnoreCase))
                throw new BadRequestException(ApiResultStatusCode.OAuth.ToDisplay());
        }


        /// <summary>
        /// this method use for creating jwt
        /// </summary>
        /// <param name="tabletUniqeId"></param>
        /// <param name="tabletData"></param>
        /// <returns></returns>
        private ActionResult Authenticate(User user, NetworkType networkType)
           => new JsonResult(_jwtService.Generate(GetClaimsAsync(user,networkType)));


        /// <summary>
        /// for create the cliams of jwt
        /// </summary>
        /// <param name="tabletUniqeId"></param>
        /// <param name="tabletData"></param>
        /// <returns></returns>
        /// <exception cref="BaseException"></exception>
        private IEnumerable<Claim> GetClaimsAsync(User user,NetworkType networkType)
        {
            try
            {
                var claims = new List<Claim>
             {
                 new(Claims.WalletAddress.ToDisplay(),user.WalletAddress ?? "no wallet"),
                 new(Claims.PublicKey.ToDisplay(),user.UserPublicKey.ToString()),
                 new(Claims.SecurityStamp.ToDisplay(),user.SecurityStamp.ToString()),
                 new(Claims.UserStatus.ToDisplay(),user.Status.ToString()),
                 new(Claims.NetworkType.ToDisplay(),networkType == NetworkType.BEP20? "BEP20":"ERC20"),
                 new(Claims.UserType.ToDisplay(),user.Role == UserRole.Customer ? UserType.User.ToString() : UserType.Admin.ToString()),
             };

                claims.AddRange(user.Permissions.Select(permission =>
                    new Claim(Claims.Permission.ToDisplay(), permission)));

                return claims;
            }
            catch (Exception ex)
            {
                throw new BaseException(ex.Message);
            }
        }


        /// <summary>
        /// for get or create user
        /// if wallet exists in db that means user is exists
        /// if does not exists should create a new user
        /// </summary>
        /// <param name="walletAddress"></param>
        /// <returns></returns>
        private async Task<User> GetOrCreateUserAsync(string walletAddress)
        {
            walletAddress = walletAddress.Trim();

            var user = await _userRepository.AsQueryable()
                .Where(u => u.WalletAddress.ToLower() == walletAddress.ToLower())
                .FirstOrDefaultAsync();

            if (user == null)
            {
                user = new User
                {
                    WalletAddress = walletAddress,
                    Role = UserRole.Customer,
                    Permissions = [],
                    UserName = null,
                    PasswordHash = null,
                    Status = UserStatus.Active,
                    LoginDates = []
                };

                await _userRepository.InsertOneAsync(user);
            }

            await AddLoginDateToUser(user);

            return user;
        }


        /// <summary>
        /// for adding last login time, just keep last 20 record
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        /// <exception cref="BaseException"></exception>
        private async Task<Domain.Collections.User> AddLoginDateToUser(Domain.Collections.User user)
        {
            try
            {
                if (user.LoginDates == null || user.LoginDates.Count == 0)
                {
                    user.LoginDates = new List<DateTime> { DateTime.UtcNow };
                    return user;
                }

                user.LoginDates.Add(DateTime.UtcNow);

                var orderedDates = user.LoginDates
                    .OrderByDescending(x => x)
                    .Take(20)
                    .ToList();

                user.LoginDates = orderedDates;

                await _userRepository.ReplaceOneAsync(user);

                return user;
            }
            catch (Exception ex)
            {
                throw new BaseException(ex.Message);
            }

        }

        private string ValidateAndConvertToChecksumAddress(string address)
        {
            var addressUtil = new AddressUtil();
            if (!addressUtil.IsValidAddressLength(address) || !addressUtil.IsChecksumAddress(address) && !address.ToLower().Equals(address))
            {
                if (!addressUtil.IsValidEthereumAddressHexFormat(address))
                {
                    throw new BadRequestException($"Invalid address format: {address}");
                }
                return addressUtil.ConvertToChecksumAddress(address);
            }
            return address;
        }



        #endregion
    }
}


