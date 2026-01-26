
namespace SLT.Services._User.DTOs.Results
{
    public class GetUserResult
    {
        public DateTime CreateMoment { get; set; }
        public string WalletAddress { get; set; }
        public ICollection<DateTime> LoginHistories { get; set; } 
    }
} 
