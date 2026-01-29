using SLT.Domain.Collections;

namespace SLT.Services._Order.DTOs.Results
{
    public class OrderListResult
    {
        public List<OrderResult> Data { get; set; } = [];
        public int PageCount { get; set; } = 0;
        public int TotalCount { get; set; } = 0;
    }
     
    public class OrderResult
    {
        public DateTime CreatedMoment { get; set; }
        public DateTime? ModifiedMoment { get; set; }
        public string OrderId { get; set; }
        public string TransferId { get; set; }

        public string OwnerWallet { get; set; }
        public string PayerWallet { get; set; }
        public List<string> SeenBy { get; set; }

        public decimal TotalAmount { get; set; }
        public string Transportation { get; set; }
        public OrderType Type { get; set; }
        public OrderState State { get; set; }
        public DateTime? PaymentDay { get; set; }

    }


}
