namespace SLT.Services._Stake.DTOs.Results
{
    public class StakeListResult
    {
        public List<StakeResult> Data { get; set; } = [];
        public int PageCount { get; set; } = 0;
        public int TotalCount { get; set; } = 0;
    }
}
