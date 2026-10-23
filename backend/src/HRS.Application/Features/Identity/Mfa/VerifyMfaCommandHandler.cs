using HRS.Application.Common.Interfaces;
using HRS.Application.Common.Models;
using HRS.Application.Features.Audit;
using HRS.Domain.Common;
using Mediator;

namespace HRS.Application.Features.Identity.Mfa;

public sealed class VerifyMfaCommandHandler(IIdentityService identityService, IAuditLog auditLog)
    : IRequestHandler<VerifyMfaCommand, Result<AuthResult>>
{
    public async ValueTask<Result<AuthResult>> Handle(VerifyMfaCommand request, CancellationToken cancellationToken)
    {
        var verified = await identityService.VerifyMfaChallengeAsync(request.ChallengeToken, request.Code, cancellationToken);

        if (verified.IsFailure)
        {
            await auditLog.RecordAsync(SignInAudit.Failed(null, "Two-step code rejected"), cancellationToken);
            return Result.Failure<AuthResult>(verified.Error);
        }

        var user = verified.Value;
        var tokens = await identityService.IssueTokensAsync(user.Id, request.IpAddress, request.UserAgent, cancellationToken);
        await auditLog.RecordAsync(SignInAudit.Succeeded(user.Id, "Signed in with two-step verification"), cancellationToken);

        return new AuthResult(user.Id, user.Name, user.Email, tokens);
    }
}
