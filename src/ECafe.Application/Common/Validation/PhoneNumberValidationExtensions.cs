using FluentValidation;

namespace ECafe.Application.Common.Validation;

public static class PhoneNumberValidationExtensions
{
    private const int MaxRawLength = 32;
    private const int MinDigitCount = 7;
    private const int MaxDigitCount = 15;
    private const string AllowedPhoneCharactersPattern = @"^\+?[0-9\s\-()]+$";

    public static IRuleBuilderOptions<T, string> MustBePhoneNumber<T>(
        this IRuleBuilder<T, string> ruleBuilder,
        string fieldName = "Phone")
        => ruleBuilder
            .NotEmpty().WithMessage($"{fieldName} is required.")
            .MaximumLength(MaxRawLength).WithMessage($"{fieldName} must be at most {MaxRawLength} characters.")
            .Matches(AllowedPhoneCharactersPattern).WithMessage($"{fieldName} format is invalid.")
            .Must(HaveValidDigitCount).WithMessage($"{fieldName} must contain between {MinDigitCount} and {MaxDigitCount} digits.");

    private static bool HaveValidDigitCount(string phone)
    {
        var digitCount = phone.Count(char.IsDigit);
        return digitCount is >= MinDigitCount and <= MaxDigitCount;
    }
}
