using Utilities.Attributes;

namespace SLT.Services._Stake.DTOs.Updates
{
    public class StakeDetailUpdate
    {
        [StringInputValidation] public string StakeReference { get; set; }
    }
}
