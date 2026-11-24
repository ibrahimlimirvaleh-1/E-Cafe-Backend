using ECafe.Application.Common.Validation;
using ECafe.Application.Repositories.User;
using ECafe.Domain.Exceptions;

namespace ECafe.Application.Services.Auth;

public static class UserRegistrationValidation
{
    /// <summary>Qeydiyyat və personal yaratmada email/telefonu eyni formatda yoxlayır; email xətası prioriteti və repository filtrləri qorunur.</summary>
    public static async Task EnsureUserDoesNotExistAsync(IUserRepository users, string email, string phone)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var normalizedPhone = PhoneNumberValidationExtensions.NormalizeAzerbaijanPhoneNumber(phone);

        if (await users.CheckExistAsync(user => user.Email == normalizedEmail))
            throw new BusinessRuleException(ErrorCode.UserEmailAlreadyExists);

        if (await users.CheckExistAsync(user => user.Phone == normalizedPhone))
            throw new BusinessRuleException(ErrorCode.UserPhoneAlreadyExists);
    }
}
