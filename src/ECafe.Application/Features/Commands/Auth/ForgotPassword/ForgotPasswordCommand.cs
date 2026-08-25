using ECafe.Application.DTOs.Auth;
using MediatR;

namespace ECafe.Application.Features.Commands.Auth.ForgotPassword;

public class ForgotPasswordCommand : ForgotPasswordRequest, IRequest
{
}
