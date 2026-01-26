using Utilities.Exceptions.Common;
using System.ComponentModel.DataAnnotations;

namespace Utilities.Attributes
{
    /// <summary>
    ///
    /// </summary>
    /// <param name="isRequired"></param>
    /// <param name="checkOffset"></param>
    /// <param name="mustBeFuture"></param>
    /// <param name="mustBePast"></param>
    /// <param name="minOffsetDays"></param>
    /// <param name="maxOffsetDays"></param>
    /// <param name="customMessage"></param>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
    public class ValidatedDateAttribute(
            bool isRequired = true,
            bool checkOffset = false,
            bool mustBeFuture = false,
            bool mustBePast = false,
            int minOffsetDays = int.MinValue,
            int maxOffsetDays = int.MinValue,
            string customMessage = null)
        : ValidationAttribute
    {
        protected override ValidationResult IsValid(object value, ValidationContext context)
        {
            if (mustBeFuture && mustBePast)
                throw new ArgumentException("Cannot require both future and past dates.");

            string displayName = context.DisplayName ?? context.MemberName!;
            string msg(string fallback)
                => !string.IsNullOrWhiteSpace(customMessage)
                    ? customMessage!
                    : $"{displayName} {fallback}";

            if (value is null)
            {
                if (isRequired)
                    throw new BadRequestException(msg("is required"));
                else
                    return ValidationResult.Success;
            }

            //Parse to DateTimeOffset
            DateTimeOffset dtoValue;
            if (value is DateTime dt)
                dtoValue = new DateTimeOffset(dt, TimeSpan.Zero);
            else if (value is DateTimeOffset dto)
                dtoValue = dto;
            else
                throw new BadRequestException($"{displayName} must be a valid date");

            if (isRequired && dtoValue == default)
                throw new BadRequestException(msg("must be a valid date"));

            var now = DateTimeOffset.UtcNow;

            if (mustBeFuture && dtoValue <= now)
                throw new BadRequestException(msg("must be in the future"));
            if (mustBePast && dtoValue >= now)
                throw new BadRequestException(msg("must be in the past"));

            if (checkOffset)
            {
                if (minOffsetDays == int.MinValue)
                    throw new BadRequestException("Min offset days not entered");

                var minBoundary = now.AddDays(minOffsetDays);
                if (dtoValue < minBoundary)
                    throw new BadRequestException(msg($"must be on or after {minBoundary:yyyy-MM-dd}"));
            }
            if (checkOffset)
            {
                if (maxOffsetDays == int.MaxValue)
                    throw new BadRequestException("Max offset days not entered");

                var maxBoundary = now.AddDays(maxOffsetDays);
                if (dtoValue > maxBoundary)
                    throw new BadRequestException(msg($"must be on or before {maxBoundary:yyyy-MM-dd}"));
            }

            return ValidationResult.Success;
        }
    }
}