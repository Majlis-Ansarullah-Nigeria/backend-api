using HRS.Application.Common.Interfaces;
using HRS.Application.Common.Models;
using HRS.Application.Features.Audit;
using HRS.Domain.Common;
using Mediator;

namespace HRS.Application.Features.Identity.Login;

public sealed class LoginCommandHandler(IIdentityService identityService, IAuditLog auditLog)
    : IRequestHandler<LoginCommand, Result<LoginOutcome>>
{
    public async ValueTask<Result<LoginOutcome>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var credentials = await identityService.ValidateCredentialsAsync(request.Identifier, request.Password, cancellationToken);

        if (credentials.IsFailure)
        {
            await auditLog.RecordAsync(SignInAudit.Failed(request.Identifier, "Password sign-in rejected"), cancellationToken);
            return Result.Failure<LoginOutcome>(credentials.Error);
        }

        var user = credentials.Value;

        if (user.TwoFactorEnabled)
        {
            var challengeToken = await identityService.CreateMfaChallengeAsync(user.Id, cancellationToken);
            return new MfaChallengeRequired(challengeToken);
        }

        var tokens = await identityService.IssueTokensAsync(user.Id, request.IpAddress, request.UserAgent, cancellationToken);
        await auditLog.RecordAsync(SignInAudit.Succeeded(user.Id, "Signed in with a password"), cancellationToken);

        return new LoggedIn(new AuthResult(user.Id, user.Name, user.Email, tokens));
    }
}
