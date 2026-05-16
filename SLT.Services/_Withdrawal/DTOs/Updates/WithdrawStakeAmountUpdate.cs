using Utilities.Attributes;

namespace SLT.Services._Withdrawal.DTOs.Updates
{
    public class WithdrawStakeAmountUpdate
    {
        [StringInputValidation] public string StakeReference { get; set; }
        [NumericInputValidation(isRequired: true, mustBeNonZero: true)] public decimal TokenAmount { get; set; }

    }
}
