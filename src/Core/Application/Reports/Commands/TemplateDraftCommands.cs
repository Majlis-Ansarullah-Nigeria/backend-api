using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Application.Common.Models;
using ManagementApi.Application.Reports.Common;
using ManagementApi.Application.Reports.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ManagementApi.Application.Reports.Commands;

/// <summary>Save changes to a draft version (name, instructions, levels and the whole structure).</summary>
public record UpdateTemplateDraftCommand(Guid TemplateId, CreateReportTemplateRequest Request) : IRequest<Result>;

/// <summary>Publish a draft version: it becomes current and every other version of the template is retired.</summary>
public record PublishTemplateCommand(Guid TemplateId) : IRequest<Result>;

public class TemplateDraftCommandHandlers :
    IRequestHandler<UpdateTemplateDraftCommand, Result>,
    IRequestHandler<PublishTemplateCommand, Result>
{
    private const string Forbidden = "FORBIDDEN: Only the National Secretariat can edit templates";

    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _clock;

    public TemplateDraftCommandHandlers(IApplicationDbContext context, ICurrentUserService currentUser, TimeProvider clock)
    {
        _context = context;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<Result> Handle(UpdateTemplateDraftCommand request, CancellationToken cancellationToken)
    {
        if (!ReportingScope.IsSecretariat(_currentUser))
        {
            return Result.Failure(Forbidden);
        }

        var template = await _context.ReportTemplates
            .Include(t => t.Sections).ThenInclude(s => s.Questions)
            .FirstOrDefaultAsync(t => t.Id == request.TemplateId, cancellationToken);

        if (template is null)
        {
            return Result.Failure("Report template not found");
        }

        var r = request.Request;
        try
        {
            var levels = r.Levels is { Count: > 0 } chosen ? chosen : template.SubmittingLevels.ToList();
            var oldSections = template.Sections.ToList();
            template.ReplaceDraft(r.Name, r.Description, levels);
            foreach (var section in oldSections)
            {
                _context.ReportQuestions.RemoveRange(section.Questions);
                _context.ReportSections.Remove(section);
            }
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return Result.Failure(ex.Message);
        }

        var error = TemplateStructure.Add(_context, template, r.Sections);
        if (error is not null)
        {
            return Result.Failure(error);
        }

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success("Draft saved");
    }

    public async Task<Result> Handle(PublishTemplateCommand request, CancellationToken cancellationToken)
    {
        if (!ReportingScope.IsSecretariat(_currentUser))
        {
            return Result.Failure(Forbidden);
        }

        var template = await _context.ReportTemplates
            .Include(t => t.Sections).ThenInclude(s => s.Questions)
            .FirstOrDefaultAsync(t => t.Id == request.TemplateId, cancellationToken);

        if (template is null)
        {
            return Result.Failure("Report template not found");
        }

        try
        {
            template.Publish(_clock.GetUtcNow().UtcDateTime);
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure(ex.Message);
        }

        // Retire every other version: no new windows on them; existing reports keep rendering with them.
        var groupId = template.VersionGroupId;
        var others = await _context.ReportTemplates
            .Where(t => t.Id != template.Id && (t.Id == groupId || t.RootTemplateId == groupId))
            .ToListAsync(cancellationToken);
        others.ForEach(t => t.Deactivate());

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success($"Version {template.Version} published");
    }
}
