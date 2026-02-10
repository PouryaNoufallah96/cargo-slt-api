namespace SLT.Services._Order.DTOs.Results
{


    public class OrderTotalReportResult
    {
        public OrderReportResult OwnerReport { get; set; }
        public OrderReportResult  PayerReport { get; set; }
         
    }
    public class OrderReportResult 
    {
        public int PendingOrderCount { get; set; }
        public int DoneOrderCount { get; set; }
        public decimal OrderProgress { get; set; }


        //public int TotalInvoiceCount { get; set; } 
        //public int InvoiceCount { get; set; }
        //public int PaidInvoiceCount { get; set; }
        //public int PendingInvoiceCount { get; set; }
        //public decimal InvoiceProgress { get; set; }
    }
}
