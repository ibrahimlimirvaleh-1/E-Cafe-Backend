using ECafe.Application.Services.Auth.Abstract;
using MediatR;

namespace ECafe.Application.Features.Commands.Auth.SetPassword
{
    public class SetPasswordCommandHandler : IRequestHandler<SetPasswordCommand>
    {
        private readonly IPasswordSetupService _passwordSetupService;

        public SetPasswordCommandHandler(IPasswordSetupService passwordSetupService)
        {
            _passwordSetupService = passwordSetupService;
        }

        public async Task Handle(SetPasswordCommand request, CancellationToken cancellationToken)
        {
            await _passwordSetupService.SetPasswordAsync(request);
        }
    }
}
