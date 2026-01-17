using FluentValidation;
using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Application.Common.Models;
using ManagementApi.Application.Members.DTOs;
using ManagementApi.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ManagementApi.Application.Members.Commands;

public record CreateMemberPositionCommand(CreateMemberPositionRequest Request) : IRequest<Result<Guid>>;

public class CreateMemberPositionCommandValidator : AbstractValidator<CreateMemberPositionCommand>
{
    public CreateMemberPositionCommandValidator()
    {
        RuleFor(x => x.Request.MemberId)
            .NotEmpty().WithMessage("MemberId is required");

        RuleFor(x => x.Request.PositionTitle)
            .NotEmpty().WithMessage("Position title is required")
            .MaximumLength(200).WithMessage("Position title cannot exceed 200 characters");

        RuleFor(x => x.Request.OrganizationLevel)
            .IsInEnum().WithMessage("Invalid organization level");

        RuleFor(x => x.Request.StartDate)
            .NotEmpty().WithMessage("Start date is required");

        RuleFor(x => x.Request.Responsibilities)
            .MaximumLength(1000).WithMessage("Responsibilities cannot exceed 1000 characters");
    }
}

public class CreateMemberPositionCommandHandler : IRequestHandler<CreateMemberPositionCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;

    public CreateMemberPositionCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<Guid>> Handle(CreateMemberPositionCommand request, CancellationToken cancellationToken)
    {
        var member = await _context.Members
            .FirstOrDefaultAsync(m => m.Id == request.Request.MemberId, cancellationToken);

        if (member == null)
        {
            return Result<Guid>.Failure($"Member with ID '{request.Request.MemberId}' not found");
        }

        var position = new MemberPosition(
            request.Request.MemberId,
            request.Request.PositionTitle,
            request.Request.OrganizationLevel,
            request.Request.OrganizationEntityId,
            request.Request.StartDate,
            request.Request.Responsibilities
        );

        _context.MemberPositions.Add(position);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(position.Id, "Member position created successfully");
    }
}
