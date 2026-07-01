using Utilities.Attributes;

namespace SLT.Services._Order.DTOs.Updates
{
    public class CreateLockedMultiStepOrderUpdate
    {
        public string Transportation { get; set; }
        public decimal TotalAmount { get; set; }
        [StringInputValidation(isRequired: false, maxLength: 42)] public string ThirdPartyApprover { get; set; }
        public List<LockedMultiStepInvoiceUpdate> Invoices { get; set; }
    }

    public class LockedMultiStepInvoiceUpdate
    {
        [StringInputValidation(maxLength: 20, minLength: 2)] public string TokenSymbol { get; set; }
        [NumericInputValidation(isRequired: true)] public decimal Amount { get; set; }
        [StringInputValidation(isRequired: false, maxLength: 500)] public string Description { get; set; }
        [NumericInputValidation(isRequired: true)] public int LockDurationMonths { get; set; }
        public DateOnly ActivationDate { get; set; }
    }
}
