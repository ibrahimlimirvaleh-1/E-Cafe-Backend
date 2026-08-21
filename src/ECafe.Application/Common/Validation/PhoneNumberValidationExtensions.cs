using FluentValidation;

namespace ECafe.Application.Common.Validation;

public static class PhoneNumberValidationExtensions
{
    private const int MaxRawLength = 32;
    private const string AllowedPhoneCharactersPattern = @"^\+?[0-9\s\-()]+$";
    private const string RequiredMessage = "{0} is required.";
    private const string MaxLengthMessage = "{0} must be at most {1} characters.";
    private const string InvalidFormatMessage = "{0} must be a valid Azerbaijan phone number. Use +994XXXXXXXXX or 0XXXXXXXXX format.";

    public static IRuleBuilderOptions<T, string> MustBePhoneNumber<T>(
        this IRuleBuilder<T, string> ruleBuilder,
        string fieldName = "Phone")
        => ruleBuilder
            .NotEmpty().WithMessage(string.Format(RequiredMessage, fieldName))
            .MaximumLength(MaxRawLength).WithMessage(string.Format(MaxLengthMessage, fieldName, MaxRawLength))
            .Matches(AllowedPhoneCharactersPattern).WithMessage($"{fieldName} format is invalid.")
            .Must(IsValidAzerbaijanPhoneNumber).WithMessage(string.Format(InvalidFormatMessage, fieldName));

    public static bool IsValidAzerbaijanPhoneNumber(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return false;

        var digits = GetDigits(phone);
        var nationalNumber = ResolveNationalNumber(digits);

        return nationalNumber is not null
               && nationalNumber.Length == 9
               && nationalNumber[0] != '0';
    }

    public static string NormalizeAzerbaijanPhoneNumber(string phone)
    {
        var digits = GetDigits(phone);
        var nationalNumber = ResolveNationalNumber(digits);

        if (nationalNumber is null || nationalNumber.Length != 9 || nationalNumber[0] == '0')
            throw new ArgumentException("Phone number must be a valid Azerbaijan phone number.", nameof(phone));

        return $"+994{nationalNumber}";
    }

    private static string GetDigits(string phone)
        => new(phone.Where(char.IsDigit).ToArray());

    private static string? ResolveNationalNumber(string digits)
    {
        if (digits.Length == 12 && digits.StartsWith("994", StringComparison.Ordinal))
            return digits[3..];

        if (digits.Length == 10 && digits.StartsWith("0", StringComparison.Ordinal))
            return digits[1..];

        return null;
    }
}
