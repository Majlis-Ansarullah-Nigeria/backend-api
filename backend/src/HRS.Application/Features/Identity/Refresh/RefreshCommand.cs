using FluentValidation;
using HRS.Application.Common.Interfaces;
using HRS.Domain.Common;
using Mediator;

namespace HRS.Application.Features.Identity.Refresh;

public sealed record RefreshCommand(string RefreshToken, string? IpAddress, string? UserAgent) : IRequest<Result<AuthTokens>>;

public sealed class RefreshCommandValidator : AbstractValidator<RefreshCommand>
{
    public RefreshCommandValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty();
    }
}

public sealed class RefreshCommandHandler(IIdentityService identityService) : IRequestHandler<RefreshCommand, Result<AuthTokens>>
{
    public async ValueTask<Result<AuthTokens>> Handle(RefreshCommand request, CancellationToken cancellationToken) =>
        await identityService.RefreshAsync(request.RefreshToken, request.IpAddress, request.UserAgent, cancellationToken);
}
