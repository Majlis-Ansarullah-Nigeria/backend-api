using HRS.Application.Common.Interfaces;
using HRS.Application.Common.Models;
using HRS.Domain.Common;
using Mediator;

namespace HRS.Application.Features.Identity.Otp;

public sealed class VerifyOtpCommandHandler(IIdentityService identityService)
    : IRequestHandler<VerifyOtpCommand, Result<LoginOutcome>>
{
    public async ValueTask<Result<LoginOutcome>> Handle(VerifyOtpCommand request, CancellationToken cancellationToken)
    {
        var verified = await identityService.VerifyOtpAsync(request.Identifier, request.Code, cancellationToken);

        if (verified.IsFailure)
        {
            return Result.Failure<LoginOutcome>(verified.Error);
        }

        var user = verified.Value;

        if (user.TwoFactorEnabled)
        {
            var challengeToken = await identityService.CreateMfaChallengeAsync(user.Id, cancellationToken);
            return new MfaChallengeRequired(challengeToken);
        }

        var tokens = await identityService.IssueTokensAsync(user.Id, request.IpAddress, request.UserAgent, cancellationToken);

        return new LoggedIn(new AuthResult(user.Id, user.Name, user.Email, tokens));
    }
}
