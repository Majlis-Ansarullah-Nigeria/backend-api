using FluentValidation;
using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Application.Common.Models;
using ManagementApi.Application.Reports.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ManagementApi.Application.Reports.Commands;

public record UpdateSubmissionWindowCommand(Guid Id, UpdateSubmissionWindowRequest Request) : IRequest<Result>;

public class UpdateSubmissionWindowCommandValidator : AbstractValidator<UpdateSubmissionWindowCommand>
{
    public UpdateSubmissionWindowCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Window ID is required");

        RuleFor(x => x.Request.Name)
            .NotEmpty().WithMessage("Name is required")
            .MaximumLength(100).WithMessage("Name must not exceed 100 characters");

        RuleFor(x => x.Request.StartDate)
            .NotEmpty().WithMessage("Start date is required");

        RuleFor(x => x.Request.EndDate)
            .NotEmpty().WithMessage("End date is required")
            .GreaterThan(x => x.Request.StartDate).WithMessage("End date must be after start date");
    }
}

public class UpdateSubmissionWindowCommandHandler : IRequestHandler<UpdateSubmissionWindowCommand, Result>
{
    private readonly IApplicationDbContext _context;

    public UpdateSubmissionWindowCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result> Handle(UpdateSubmissionWindowCommand request, CancellationToken cancellationToken)
    {
        var window = await _context.SubmissionWindows
            .FirstOrDefaultAsync(w => w.Id == request.Id, cancellationToken);

        if (window == null)
        {
            return Result.Failure("Submission window not found");
        }

        window.UpdateWindow(
            request.Request.Name,
            request.Request.StartDate,
            request.Request.EndDate,
            request.Request.Description
        );

        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success("Submission window updated successfully");
    }
}
