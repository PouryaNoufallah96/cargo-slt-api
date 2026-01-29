using Utilities.Attributes;
using Utilities.MongoDatabase.Documents;

namespace SLT.Domain.Collections
{

    [MonjoCollectionName("Orders")]
    public class Order : BaseDocument
    {
        public string OrderId { get; set; } = Guid.NewGuid().ToString("N");
        public string TransferId { get; set; }

        public string OwnerWallet { get; set; }
        public string PayerWallet { get; set; }
        public List<string> SeenBy { get; set; } = [];


        public decimal TotalAmount { get; set; }
        public string Transportation { get; set; }
        public OrderType Type { get; set; }
        public OrderState State { get; set; } 
        public DateTime? PaymentDay { get; set; }
    }


    public enum OrderType { Quick, Multi }
    public enum OrderState { Pending, Completed }



}
