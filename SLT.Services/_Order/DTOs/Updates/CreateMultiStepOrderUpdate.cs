using Utilities.Attributes;

namespace SLT.Services._Order.DTOs.Updates
{
    public class CreateMultiStepOrderUpdate
    {
        public string Transportation { get; set; }
        public decimal TotalAmount { get; set; }
        public bool IsLocked { get; set; }
        [StringInputValidation(isRequired: false, maxLength: 42)] public string ThirdPartyApprover { get; set; }
        public List<MultiStepInvoiceUpdate> Invoices { get; set; }
    }



     
    public class MultiStepInvoiceUpdate
    {
        [StringInputValidation(maxLength: 20, minLength: 2)] public string TokenSymbol { get; set; }
        [NumericInputValidation(isRequired: true)] public decimal Amount { get; set; }
        [StringInputValidation(isRequired: false, maxLength: 500)] public string Description { get; set; }
        public int? LockDurationMonths { get; set; }
        public DateOnly ActivationDate { get; set; } 
    }

}
