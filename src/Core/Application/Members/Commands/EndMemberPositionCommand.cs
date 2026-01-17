using FluentValidation;
using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Application.Common.Models;
using ManagementApi.Application.Members.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ManagementApi.Application.Members.Commands;

public record EndMemberPositionCommand(EndMemberPositionRequest Request) : IRequest<Result>;

public class EndMemberPositionCommandValidator : AbstractValidator<EndMemberPositionCommand>
{
    public EndMemberPositionCommandValidator()
    {
        RuleFor(x => x.Request.Id)
            .NotEmpty().WithMessage("Position ID is required");

        RuleFor(x => x.Request.EndDate)
            .NotEmpty().WithMessage("End date is required");
    }
}

public class EndMemberPositionCommandHandler : IRequestHandler<EndMemberPositionCommand, Result>
{
    private readonly IApplicationDbContext _context;

    public EndMemberPositionCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result> Handle(EndMemberPositionCommand request, CancellationToken cancellationToken)
    {
        var position = await _context.MemberPositions
            .FirstOrDefaultAsync(mp => mp.Id == request.Request.Id, cancellationToken);

        if (position == null)
        {
            return Result.Failure($"Member position with ID '{request.Request.Id}' not found");
        }

        if (!position.IsActive)
        {
            return Result.Failure("This position has already been ended");
        }

        position.EndPosition(request.Request.EndDate);

        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success("Member position ended successfully");
    }
}
