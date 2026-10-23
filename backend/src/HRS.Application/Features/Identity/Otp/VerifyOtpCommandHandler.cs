using HRS.Application.Common.Interfaces;
using HRS.Application.Common.Models;
using HRS.Application.Features.Audit;
using HRS.Domain.Common;
using Mediator;

namespace HRS.Application.Features.Identity.Otp;

public sealed class VerifyOtpCommandHandler(IIdentityService identityService, IAuditLog auditLog)
    : IRequestHandler<VerifyOtpCommand, Result<LoginOutcome>>
{
    public async ValueTask<Result<LoginOutcome>> Handle(VerifyOtpCommand request, CancellationToken cancellationToken)
    {
        var verified = await identityService.VerifyOtpAsync(request.Identifier, request.Code, cancellationToken);

        if (verified.IsFailure)
        {
            await auditLog.RecordAsync(SignInAudit.Failed(request.Identifier, "Email code rejected"), cancellationToken);
            return Result.Failure<LoginOutcome>(verified.Error);
        }

        var user = verified.Value;

        if (user.TwoFactorEnabled)
        {
            var challengeToken = await identityService.CreateMfaChallengeAsync(user.Id, cancellationToken);
            return new MfaChallengeRequired(challengeToken);
        }

        var tokens = await identityService.IssueTokensAsync(user.Id, request.IpAddress, request.UserAgent, cancellationToken);
        await auditLog.RecordAsync(SignInAudit.Succeeded(user.Id, "Signed in with an email code"), cancellationToken);

        return new LoggedIn(new AuthResult(user.Id, user.Name, user.Email, tokens));
    }
}
