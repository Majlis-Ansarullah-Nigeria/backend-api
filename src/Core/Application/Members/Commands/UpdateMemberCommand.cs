using FluentValidation;
using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Application.Common.Models;
using ManagementApi.Application.Members.DTOs;
using ManagementApi.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ManagementApi.Application.Members.Commands;

public record UpdateMemberCommand(UpdateMemberRequest Request) : IRequest<Result>;

public class UpdateMemberCommandValidator : AbstractValidator<UpdateMemberCommand>
{
    public UpdateMemberCommandValidator()
    {
        RuleFor(x => x.Request.Id)
            .NotEmpty().WithMessage("Member ID is required");

        RuleFor(x => x.Request.Surname)
            .NotEmpty().WithMessage("Surname is required")
            .MaximumLength(100).WithMessage("Surname cannot exceed 100 characters");

        RuleFor(x => x.Request.FirstName)
            .MaximumLength(100).WithMessage("First name cannot exceed 100 characters");

        RuleFor(x => x.Request.MiddleName)
            .MaximumLength(100).WithMessage("Middle name cannot exceed 100 characters");

        RuleFor(x => x.Request.Email)
            .EmailAddress().When(x => !string.IsNullOrEmpty(x.Request.Email))
            .WithMessage("Invalid email address");

        RuleFor(x => x.Request.PhoneNo)
            .MaximumLength(100).WithMessage("Phone number cannot exceed 100 characters");

        RuleFor(x => x.Request.Address)
            .MaximumLength(500).WithMessage("Address cannot exceed 500 characters");
    }
}

public class UpdateMemberCommandHandler : IRequestHandler<UpdateMemberCommand, Result>
{
    private readonly IApplicationDbContext _context;

    public UpdateMemberCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result> Handle(UpdateMemberCommand request, CancellationToken cancellationToken)
    {
        var member = await _context.Members
            .FirstOrDefaultAsync(m => m.Id == request.Request.Id, cancellationToken);

        if (member == null)
        {
            return Result.Failure($"Member with ID '{request.Request.Id}' not found");
        }

        // Update basic information
        member.UpdateBasicInfo(
            request.Request.Surname,
            request.Request.FirstName,
            request.Request.MiddleName,
            request.Request.DateOfBirth,
            request.Request.Email,
            request.Request.PhoneNo,
            request.Request.MaritalStatus,
            request.Request.Address
        );

        // Update medical information
        member.UpdateMedicalInfo(
            request.Request.BloodGroup,
            request.Request.Genotype
        );

        // Update Jamaat assignment if provided
        if (request.Request.JamaatId.HasValue)
        {
            member.UpdateJamaatId(request.Request.JamaatId.Value);
        }

        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success("Member updated successfully");
    }
}
