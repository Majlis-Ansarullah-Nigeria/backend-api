using FluentValidation;
using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Application.Common.Models;
using ManagementApi.Application.Members.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ManagementApi.Application.Members.Commands;

public record UpdateMemberPositionCommand(UpdateMemberPositionRequest Request) : IRequest<Result>;

public class UpdateMemberPositionCommandValidator : AbstractValidator<UpdateMemberPositionCommand>
{
    public UpdateMemberPositionCommandValidator()
    {
        RuleFor(x => x.Request.Id)
            .NotEmpty().WithMessage("Position ID is required");

        RuleFor(x => x.Request.PositionTitle)
            .NotEmpty().WithMessage("Position title is required")
            .MaximumLength(200).WithMessage("Position title cannot exceed 200 characters");

        RuleFor(x => x.Request.Responsibilities)
            .MaximumLength(1000).WithMessage("Responsibilities cannot exceed 1000 characters");
    }
}

public class UpdateMemberPositionCommandHandler : IRequestHandler<UpdateMemberPositionCommand, Result>
{
    private readonly IApplicationDbContext _context;

    public UpdateMemberPositionCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result> Handle(UpdateMemberPositionCommand request, CancellationToken cancellationToken)
    {
        var position = await _context.MemberPositions
            .FirstOrDefaultAsync(mp => mp.Id == request.Request.Id, cancellationToken);

        if (position == null)
        {
            return Result.Failure($"Member position with ID '{request.Request.Id}' not found");
        }

        position.UpdatePosition(request.Request.PositionTitle, request.Request.Responsibilities);

        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success("Member position updated successfully");
    }
}
