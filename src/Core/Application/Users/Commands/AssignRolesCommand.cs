using FluentValidation;
using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Application.Common.Models;
using ManagementApi.Application.Users.DTOs;
using MediatR;

namespace ManagementApi.Application.Users.Commands;

public record AssignRolesCommand(AssignRolesRequest Request) : IRequest<Result>;

public class AssignRolesCommandValidator : AbstractValidator<AssignRolesCommand>
{
    public AssignRolesCommandValidator()
    {
        RuleFor(x => x.Request.UserId)
            .NotEmpty().WithMessage("User ID is required");

        RuleFor(x => x.Request.Roles)
            .NotEmpty().WithMessage("At least one role must be specified");
    }
}

public class AssignRolesCommandHandler : IRequestHandler<AssignRolesCommand, Result>
{
    private readonly IIdentityService _identityService;

    public AssignRolesCommandHandler(IIdentityService identityService)
    {
        _identityService = identityService;
    }

    public async Task<Result> Handle(AssignRolesCommand request, CancellationToken cancellationToken)
    {
        var result = await _identityService.AssignRolesToUserAsync(
            request.Request.UserId,
            request.Request.Roles
        );

        return result;
    }
}
