
namespace Utilities.Constants
{
   

    #region JWT Settings
    public class JwtServiceSettings
    {
        public string Issuer { get; set; }
        public string Audience { get; set; }
        public string SignatureKey { get; set; }
        public string EncryptionKey { get; set; }
        public int ExpiresAfter { get; set; }
        public Dictionary<string, string> ClientInfo { get; set; }
    }
    #endregion

    #region FireWall Settings
    public class FirewallSettings
    {
        public FirewallRule[] Rules { get; set; }
    }
    public class FirewallRule
    {
        public string Regex { get; set; }
        public string[] IPAddresses { get; set; }
        public FirewallRulePolicy Policy { get; set; }
    }
    public enum FirewallRulePolicy { Allow, Deny }
    #endregion

    #region Captcha Settings
    public class CaptchaSettings
    {
        public string BaseUrl { get; set; }
    }
    #endregion

    
}