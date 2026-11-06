using FluentValidation;
using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Application.Common.Models;
using ManagementApi.Application.Reports.Common;
using ManagementApi.Application.Reports.DTOs;
using ManagementApi.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ManagementApi.Application.Reports.Commands;

/// <summary>Gives one unit more time in a window. National Secretariat only.</summary>
public record GrantExtensionCommand(Guid WindowId, GrantExtensionRequest Request) : IRequest<Result<Guid>>;

public record RemoveExtensionCommand(Guid WindowId, Guid ExtensionId) : IRequest<Result>;

public class GrantExtensionCommandValidator : AbstractValidator<GrantExtensionCommand>
{
    public GrantExtensionCommandValidator()
    {
        RuleFor(x => x.Request.UnitId).NotEmpty().WithMessage("Choose the unit to extend");
        RuleFor(x => x.Request.OrganizationLevel)
            .Must(l => l is OrganizationLevel.Muqam or OrganizationLevel.Dila or OrganizationLevel.Zone)
            .WithMessage("Extensions are for a Muqam, Dila or Zone");
        RuleFor(x => x.Request.Reason)
            .NotEmpty().WithMessage("A reason is required for an extension")
            .MaximumLength(500);
    }
}

public class WindowExtensionCommandHandlers :
    IRequestHandler<GrantExtensionCommand, Result<Guid>>,
    IRequestHandler<RemoveExtensionCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public WindowExtensionCommandHandlers(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<Guid>> Handle(GrantExtensionCommand request, CancellationToken cancellationToken)
    {
        if (!ReportingScope.IsSecretariat(_currentUser))
        {
            return Result<Guid>.Failure("FORBIDDEN: Only the National Secretariat can grant extensions");
        }

        var window = await _context.SubmissionWindows
            .Include(w => w.Extensions)
            .FirstOrDefaultAsync(w => w.Id == request.WindowId, cancellationToken);

        if (window is null)
        {
            return Result<Guid>.Failure("Submission window not found");
        }

        var r = request.Request;
        var unitExists = r.OrganizationLevel switch
        {
            OrganizationLevel.Muqam => await _context.Muqams.AnyAsync(m => m.Id == r.UnitId, cancellationToken),
            OrganizationLevel.Dila => await _context.Dilas.AnyAsync(d => d.Id == r.UnitId, cancellationToken),
            OrganizationLevel.Zone => await _context.Zones.AnyAsync(z => z.Id == r.UnitId, cancellationToken),
            _ => false
        };

        if (!unitExists)
        {
            return Result<Guid>.Failure($"{r.OrganizationLevel} not found");
        }

        try
        {
            var extension = window.GrantExtension(r.OrganizationLevel, r.UnitId, r.DueAt, r.ClosesAt, r.Reason);
            await _context.SaveChangesAsync(cancellationToken);
            return Result<Guid>.Success(extension.Id, "Extension granted");
        }
        catch (ArgumentException ex)
        {
            return Result<Guid>.Failure(ex.Message);
        }
    }

    public async Task<Result> Handle(RemoveExtensionCommand request, CancellationToken cancellationToken)
    {
        if (!ReportingScope.IsSecretariat(_currentUser))
        {
            return Result.Failure("FORBIDDEN: Only the National Secretariat can remove extensions");
        }

        var window = await _context.SubmissionWindows
            .Include(w => w.Extensions)
            .FirstOrDefaultAsync(w => w.Id == request.WindowId, cancellationToken);

        if (window is null)
        {
            return Result.Failure("Submission window not found");
        }

        window.RemoveExtension(request.ExtensionId);
        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success("Extension removed");
    }
}
