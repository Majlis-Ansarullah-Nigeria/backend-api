using HRS.Application.Common.Interfaces;
using HRS.Domain.Common;
using Mediator;

namespace HRS.Application.Features.Identity.PasswordReset;

public sealed record ResetPasswordCommand(string Email, string Token, string NewPassword) : IRequest<Result>;

public sealed class ResetPasswordCommandHandler(IIdentityService identityService)
    : IRequestHandler<ResetPasswordCommand, Result>
{
    public ValueTask<Result> Handle(ResetPasswordCommand request, CancellationToken cancellationToken) =>
        new(identityService.ResetPasswordAsync(request.Email, request.Token, request.NewPassword, cancellationToken));
}
