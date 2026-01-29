using SLT.Domain.Collections;
using Utilities.DTOs;

namespace SLT.Services._Order.DTOs.Updates
{
    public class GetPendingOrderListUpdate
    {
        public Pagination Pagination { get; set; } = new Pagination();
        public OrderListType ListType { get; set; } = OrderListType.Received;
        public OrderState State { get; set; } = OrderState.Pending;
    } 
     
    public enum OrderListType { Received, Sent }


}
