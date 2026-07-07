namespace SLT.Services._Order.DTOs.Results
{
    public class ApprovalListResult
    {
        public List<InvoiceResult> Data { get; set; } = [];
        public int PageCount { get; set; } = 0;
        public int TotalCount { get; set; } = 0;
    }
}
