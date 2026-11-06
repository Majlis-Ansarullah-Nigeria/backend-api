using FluentValidation;
using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Application.Common.Models;
using ManagementApi.Application.Reports.Common;
using ManagementApi.Application.Reports.DTOs;
using ManagementApi.Domain.Entities.Reports;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ManagementApi.Application.Reports.Commands;

/// <summary>
/// Publishes a changed template as the next version. Published versions are never edited:
/// earlier versions are retired, and reports and open windows keep the version they use.
/// National Secretariat only.
/// </summary>
public record CreateTemplateVersionCommand(Guid TemplateId, CreateReportTemplateRequest Request) : IRequest<Result<Guid>>;

public class CreateTemplateVersionCommandValidator : AbstractValidator<CreateTemplateVersionCommand>
{
    public CreateTemplateVersionCommandValidator()
    {
        RuleFor(x => x.Request.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Request.Sections).NotEmpty().WithMessage("A template needs at least one section");
    }
}

public class CreateTemplateVersionCommandHandler : IRequestHandler<CreateTemplateVersionCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public CreateTemplateVersionCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<Guid>> Handle(CreateTemplateVersionCommand request, CancellationToken cancellationToken)
    {
        if (!ReportingScope.IsSecretariat(_currentUser))
        {
            return Result<Guid>.Failure("FORBIDDEN: Only the National Secretariat can publish template versions");
        }

        var previous = await _context.ReportTemplates
            .FirstOrDefaultAsync(t => t.Id == request.TemplateId, cancellationToken);

        if (previous is null)
        {
            return Result<Guid>.Failure("Report template not found");
        }

        var groupId = previous.VersionGroupId;
        var versions = await _context.ReportTemplates
            .Where(t => t.Id == groupId || t.RootTemplateId == groupId)
            .ToListAsync(cancellationToken);

        var r = request.Request;
        var next = new ReportTemplate(r.Name, previous.ReportType, r.OrganizationLevel, r.IsForAllMembers, r.Description, r.DisplayOrder);
        next.BecomeNextVersionOf(previous, versions.Max(v => v.Version) + 1);
        _context.ReportTemplates.Add(next);

        var error = TemplateStructure.Add(_context, next, r.Sections);
        if (error is not null)
        {
            return Result<Guid>.Failure(error);
        }

        // Retire every earlier version: no new windows on them, but existing reports keep rendering with them.
        versions.ForEach(v => v.Deactivate());

        await _context.SaveChangesAsync(cancellationToken);
        return Result<Guid>.Success(next.Id, $"Version {next.Version} published");
    }
}
