namespace SLT.Services._User.DTOs.Updates
{
    public class NonceResult
    {
        public string Nonce { get; set; }
        public string Code { get; set; } = null;
        public string Message { get; set; }
        public bool ShouldVerify { get; set; } = false;
        public DateTime ExpireMoment { get; set; }  
    }
}
