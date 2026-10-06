using HRS.Application.Common.Interfaces;
using HRS.Application.Common.Models;
using HRS.Domain.Common;
using Mediator;

namespace HRS.Application.Features.Identity.Login;

public sealed class LoginCommandHandler(IIdentityService identityService)
    : IRequestHandler<LoginCommand, Result<LoginOutcome>>
{
    public async ValueTask<Result<LoginOutcome>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var credentials = await identityService.ValidateCredentialsAsync(request.Identifier, request.Password, cancellationToken);

        if (credentials.IsFailure)
        {
            return Result.Failure<LoginOutcome>(credentials.Error);
        }

        var user = credentials.Value;

        if (user.TwoFactorEnabled)
        {
            var challengeToken = await identityService.CreateMfaChallengeAsync(user.Id, cancellationToken);
            return new MfaChallengeRequired(challengeToken);
        }

        var tokens = await identityService.IssueTokensAsync(user.Id, request.IpAddress, request.UserAgent, cancellationToken);

        return new LoggedIn(new AuthResult(user.Id, user.Name, user.Email, tokens));
    }
}
