using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace Utilities.Attributes
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
    public class GuidInputValidationAttribute(
        bool isRequired = true,
        string errorMessage = null
    ) : ValidationAttribute
    {
        private const string DefaultPattern = "^[0-9A-Fa-f]{32}$";
        private static readonly Regex GuidRegex = new Regex(DefaultPattern, RegexOptions.Compiled);

        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            string displayName = validationContext.DisplayName ?? validationContext.MemberName;
            string strValue = value as string;

            // Required check
            if (string.IsNullOrEmpty(strValue))
            {
                if (isRequired)
                    return new ValidationResult(errorMessage ?? $"{displayName} is required.");
                return ValidationResult.Success;
            }

            // Format check: 32 hex digits ("N" format)
            if (!GuidRegex.IsMatch(strValue))
            {
                return new ValidationResult(errorMessage ?? $"{displayName} must be a valid 32-character GUID (no hyphens)."
                );
            }

            return ValidationResult.Success;
        }
    }
}