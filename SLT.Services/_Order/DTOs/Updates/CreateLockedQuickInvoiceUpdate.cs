using Utilities.Attributes;

namespace SLT.Services._Order.DTOs.Updates
{
    public class CreateLockedQuickInvoiceUpdate
    {
        [StringInputValidation(maxLength: 20, minLength: 2)] public string TokenSymbol { get; set; }
        [NumericInputValidation(isRequired: true)] public decimal Amount { get; set; }
        [StringInputValidation(isRequired: false, maxLength: 500)] public string Description { get; set; }
        [NumericInputValidation(isRequired: true)] public int LockDurationMonths { get; set; }
        [StringInputValidation(isRequired: false, maxLength: 42)] public string ThirdPartyApprover { get; set; }
    }
}
