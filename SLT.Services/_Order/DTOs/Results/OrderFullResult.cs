using SLT.Domain.Collections;

namespace SLT.Services._Order.DTOs.Results
{
    public class OrderFullResult : OrderResult
    {   
        public List<InvoiceResult> Invoices { get; set; }
         
    } 


    public class InvoiceResult
    {
      
        public DateTime CreatedMoment { get; set; }
        public DateTime? ModifiedMoment { get; set; }
        public string InvoiceId { get; set; } 
        public string OwnerWallet { get; set; }
        public string PayerWallet { get; set; }
        public string OrderId { get; set; }
        public string TokenSybmol { get; set; }
        public string TokenAddress { get; set; }
        public decimal USDTAmount { get; set; }
        public string USDTAmountInWei { get; set; }
        public string Desctiption { get; set; }

        public decimal? TokenAmountAtPayment { get; set; }
        public string? TokenAmountWeiAtPayment { get; set; }
        public decimal? TokenPriceAtPayment { get; set; }

        public InvoiceState State { get; set; } = InvoiceState.Pending;
        public DateTime? PayMoment { get; set; } = null;
        public string RegisterHash { get; set; } = null;
        public string PaymentHash { get; set; } = null;
        public DateTime? ActivateDate { get; set; } = null; 
    }


}
