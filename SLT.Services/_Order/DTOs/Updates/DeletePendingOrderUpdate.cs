using Utilities.Attributes;

namespace SLT.Services._Order.DTOs.Updates
{
    public class DeletePendingOrderUpdate
    {
        [StringInputValidation]public string OrderId { get; set; }
    }
}
