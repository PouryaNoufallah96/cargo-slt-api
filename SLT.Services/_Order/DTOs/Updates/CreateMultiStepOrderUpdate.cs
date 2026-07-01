namespace SLT.Services._Order.DTOs.Updates
{
    public class CreateMultiStepOrderUpdate
    {
        public string Transportation { get; set; }
        public decimal TotalAmount { get; set; }
        public List<MultiStepInvoiceUpdate> Invoices { get; set; }
    }



     
    public class MultiStepInvoiceUpdate : CreateQuickInvoiceUpdate
    {
        public DateOnly ActivationDate { get; set; } 
    }

}
