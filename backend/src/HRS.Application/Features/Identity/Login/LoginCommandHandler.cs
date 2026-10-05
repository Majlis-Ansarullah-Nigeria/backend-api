using HRS.Application.Common.Interfaces;
using HRS.Domain.Common;
using Mediator;

namespace HRS.Application.Features.Identity.Login;

public sealed class LoginCommandHandler(IIdentityService identityService) : IRequestHandler<LoginCommand, Result<LoginResult>>
{
    public async ValueTask<Result<LoginResult>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var credentials = await identityService.ValidateCredentialsAsync(request.Identifier, request.Password, cancellationToken);

        if (credentials.IsFailure)
        {
            return Result.Failure<LoginResult>(credentials.Error);
        }

        var user = credentials.Value;
        var tokens = await identityService.IssueTokensAsync(user.Id, request.IpAddress, request.UserAgent, cancellationToken);

        return new LoginResult(user.Id, user.Name, user.Email, tokens);
    }
}
