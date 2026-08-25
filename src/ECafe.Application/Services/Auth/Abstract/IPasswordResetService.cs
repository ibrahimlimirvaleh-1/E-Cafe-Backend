using ECafe.Application.DTOs.Auth;

namespace ECafe.Application.Services.Auth.Abstract;

public interface IPasswordResetService
{
    Task RequestPasswordResetAsync(ForgotPasswordRequest request);

    Task ResetPasswordAsync(ResetPasswordRequest request);
}
