using ECafe.Application.DTOs.Auth;

namespace ECafe.Application.Services.Auth.Abstract;

public interface IEmailChangeService
{
    Task RequestAsync(RequestEmailChangeRequest request, CancellationToken cancellationToken = default);
    Task ConfirmAsync(ConfirmEmailChangeRequest request, CancellationToken cancellationToken = default);
}
