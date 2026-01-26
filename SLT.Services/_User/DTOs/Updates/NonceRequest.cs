using Utilities.Attributes;

namespace SLT.Services._User.DTOs.Updates
{
   
    public class NonceRequest
    {
        [StringInputValidation(maxLength:200)] public string WalletAddress { get; set; }
        [StringInputValidation(maxLength: 50)] public string ClientId { get; set; }
        [StringInputValidation(maxLength: 50)] public string ClientSecret { get; set; }
    }
}
