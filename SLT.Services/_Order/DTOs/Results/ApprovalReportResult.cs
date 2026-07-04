namespace SLT.Services._Order.DTOs.Results
{
    public class ApprovalReportResult
    {
        public int PendingApprovalCount { get; set; }
        public int DoneApprovalCount { get; set; }
        public decimal ApprovalProgress { get; set; }
    }
}
