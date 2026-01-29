using Utilities.Attributes;

namespace SLT.Services._Order.DTOs.Updates
{
    public class CreateQuickInvoiceUpdate
    {
        [StringInputValidation(maxLength: 20, minLength: 2)] public string TokenSymbol { get; set; }
        [NumericInputValidation(isRequired:true)] public decimal Amount { get; set; }
        [StringInputValidation( isRequired:false,maxLength: 500)] public string Description { get; set; }
    }
}
 