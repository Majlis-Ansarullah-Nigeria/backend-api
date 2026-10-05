using HRS.Application.Common.Interfaces;
using HRS.Application.Common.Models;
using HRS.Domain.Common;
using Mediator;

namespace HRS.Application.Features.Identity.Otp;

public sealed class VerifyOtpCommandHandler(IIdentityService identityService)
    : IRequestHandler<VerifyOtpCommand, Result<AuthResult>>
{
    public async ValueTask<Result<AuthResult>> Handle(VerifyOtpCommand request, CancellationToken cancellationToken)
    {
        var verified = await identityService.VerifyOtpAsync(request.Identifier, request.Code, cancellationToken);

        if (verified.IsFailure)
        {
            return Result.Failure<AuthResult>(verified.Error);
        }

        var user = verified.Value;
        var tokens = await identityService.IssueTokensAsync(user.Id, request.IpAddress, request.UserAgent, cancellationToken);

        return new AuthResult(user.Id, user.Name, user.Email, tokens);
    }
}
