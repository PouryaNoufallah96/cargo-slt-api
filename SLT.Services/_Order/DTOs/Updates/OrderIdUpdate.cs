using Utilities.Attributes;

namespace SLT.Services._Order.DTOs.Updates
{
    public class OrderIdUpdate
    {
        [StringInputValidation(maxLength: 32, minLength: 10)] public string OrderOrTransferId { get; set; }
    }
}
