using Utilities.Attributes;

namespace SLT.Services._Stake.DTOs.Updates
{
    public class CreateStakeUpdate
    {
        [StringInputValidation]public string Symbol { get; set; }
        [NumericInputValidation(isRequired:true,mustBeNonZero:true,mustBePositive:true)]public decimal Amount { get; set; }
        [NumericInputValidation(isRequired: true, mustBeNonZero: true, mustBePositive: true)] public int Duration { get; set; }
    }
}
