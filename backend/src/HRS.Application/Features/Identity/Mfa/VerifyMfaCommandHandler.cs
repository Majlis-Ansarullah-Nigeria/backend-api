using HRS.Application.Common.Interfaces;
using HRS.Application.Common.Models;
using HRS.Domain.Common;
using Mediator;

namespace HRS.Application.Features.Identity.Mfa;

public sealed class VerifyMfaCommandHandler(IIdentityService identityService)
    : IRequestHandler<VerifyMfaCommand, Result<AuthResult>>
{
    public async ValueTask<Result<AuthResult>> Handle(VerifyMfaCommand request, CancellationToken cancellationToken)
    {
        var verified = await identityService.VerifyMfaChallengeAsync(request.ChallengeToken, request.Code, cancellationToken);

        if (verified.IsFailure)
        {
            return Result.Failure<AuthResult>(verified.Error);
        }

        var user = verified.Value;
        var tokens = await identityService.IssueTokensAsync(user.Id, request.IpAddress, request.UserAgent, cancellationToken);

        return new AuthResult(user.Id, user.Name, user.Email, tokens);
    }
}
