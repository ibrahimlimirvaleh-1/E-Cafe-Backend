using ECafe.Application.DTOs.Auth;
using MediatR;

namespace ECafe.Application.Features.Commands.Auth.ResetPassword;

public class ResetPasswordCommand : ResetPasswordRequest, IRequest
{
}
