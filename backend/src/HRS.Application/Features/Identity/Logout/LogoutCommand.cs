using FluentValidation;
using HRS.Application.Common.Interfaces;
using HRS.Domain.Common;
using Mediator;

namespace HRS.Application.Features.Identity.Logout;

public sealed record LogoutCommand(string RefreshToken) : IRequest<Result>;

public sealed class LogoutCommandValidator : AbstractValidator<LogoutCommand>
{
    public LogoutCommandValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty();
    }
}

public sealed class LogoutCommandHandler(IIdentityService identityService) : IRequestHandler<LogoutCommand, Result>
{
    public async ValueTask<Result> Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        await identityService.RevokeSessionAsync(request.RefreshToken, cancellationToken);
        return Result.Success();
    }
}
