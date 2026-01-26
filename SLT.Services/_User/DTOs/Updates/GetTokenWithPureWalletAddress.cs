using Utilities.Attributes;

namespace SLT.Services._User.DTOs.Updates
{
    public class GetTokenWithPureWalletAddress
    {
        [StringInputValidation] public string WalletAddress { get; set; }
        [StringInputValidation] public string ClientId { get; set; }
        [StringInputValidation] public string ClientSecret { get; set; }
    }
}
