using ECafe.Application.DTOs.Auth;

namespace ECafe.Application.Services.Auth.Abstract
{
    public interface IPasswordSetupService
    {
        Task SendSetupLinkAsync(Domain.Entities.User user);

        Task SetPasswordAsync(SetPasswordRequest request);
    }
}
