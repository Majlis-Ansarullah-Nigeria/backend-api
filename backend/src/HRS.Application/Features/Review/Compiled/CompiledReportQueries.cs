using FluentValidation;
using HRS.Application.Common.Interfaces;
using HRS.Application.Features.Audit;
using HRS.Domain.Common;
using HRS.Domain.Review;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Review.Compiled;

/// <summary>Every window that has opened, newest first — each can be compiled.</summary>
public sealed record GetCompiledReportsQuery(Guid UserId) : IRequest<Result<IReadOnlyList<CompiledReportListItem>>>;

public sealed class GetCompiledReportsQueryHandler(IAppDbContext db, IAccessPolicy accessPolicy, IClock clock)
    : IRequestHandler<GetCompiledReportsQuery, Result<IReadOnlyList<CompiledReportListItem>>>
{
    public async ValueTask<Result<IReadOnlyList<CompiledReportListItem>>> Handle(GetCompiledReportsQuery request, CancellationToken cancellationToken)
    {
        var grant = await CompiledReportAccess.ResolveAsync(accessPolicy, request.UserId, cancellationToken);
        if (!grant.CanView)
        {
            return Result.Failure<IReadOnlyList<CompiledReportListItem>>(Error.Forbidden("FORBIDDEN", "Only the Secretariat and Leadership see compiled reports."));
        }

        var now = clock.UtcNow;
        var withNarrative = (await db.CompiledReports.AsNoTracking().Where(c => c.Narrative != "").Select(c => c.WindowId).ToListAsync(cancellationToken)).ToHashSet();
        var items = await db.SubmissionWindows.AsNoTracking()
            .Where(w => w.OpensAt <= now)
            .Join(db.ReportTypes.AsNoTracking().Where(r => r.ArchivedAt == null), w => w.ReportTypeId, r => r.Id,
                (w, r) => new { w.Id, ReportTypeId = r.Id, ReportTypeName = r.Name, w.PeriodLabel, w.OpensAt, w.ClosesAt })
            .OrderByDescending(w => w.OpensAt)
            .Take(100)
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<CompiledReportListItem>>([.. items.Select(w => new CompiledReportListItem(
            w.Id, w.ReportTypeId, w.ReportTypeName, w.PeriodLabel, w.OpensAt, now >= w.ClosesAt, withNarrative.Contains(w.Id)))]);
    }
}

/// <summary>FR-REV-006: the compiled national report for one window, built live.</summary>
public sealed record GetCompiledReportQuery(Guid UserId, Guid WindowId) : IRequest<Result<CompiledReportView>>;

public sealed class GetCompiledReportQueryHandler(IAppDbContext db, IAccessPolicy accessPolicy, IClock clock)
    : IRequestHandler<GetCompiledReportQuery, Result<CompiledReportView>>
{
    public async ValueTask<Result<CompiledReportView>> Handle(GetCompiledReportQuery request, CancellationToken cancellationToken)
    {
        var grant = await CompiledReportAccess.ResolveAsync(accessPolicy, request.UserId, cancellationToken);
        if (!grant.CanView)
        {
            return Result.Failure<CompiledReportView>(Error.Forbidden("FORBIDDEN", "Only the Secretariat and Leadership see compiled reports."));
        }

        var window = await db.SubmissionWindows.AsNoTracking().FirstOrDefaultAsync(w => w.Id == request.WindowId, cancellationToken);
        if (window is null)
        {
            return Result.Failure<CompiledReportView>(Error.NotFound("WINDOW_NOT_FOUND", "That window doesn't exist."));
        }

        return Result.Success(await CompiledReportBuilder.BuildAsync(db, window, grant.CanEdit, clock.UtcNow, cancellationToken));
    }
}

public enum CompiledReportFormat
{
    Pdf,
    Word,
}

public sealed record CompiledReportFile(byte[] Content, string ContentType, string FileName);

/// <summary>FR-REV-006's "exportable to Word and PDF" — the same live report, rendered.</summary>
public sealed record ExportCompiledReportQuery(Guid UserId, Guid WindowId, CompiledReportFormat Format) : IRequest<Result<CompiledReportFile>>;

public sealed class ExportCompiledReportQueryHandler(
    IAppDbContext db, IAccessPolicy accessPolicy, IClock clock, ICompiledReportRenderer renderer, IOrganizationBrandingProvider brandingProvider)
    : IRequestHandler<ExportCompiledReportQuery, Result<CompiledReportFile>>
{
    public async ValueTask<Result<CompiledReportFile>> Handle(ExportCompiledReportQuery request, CancellationToken cancellationToken)
    {
        var view = await new GetCompiledReportQueryHandler(db, accessPolicy, clock)
            .Handle(new GetCompiledReportQuery(request.UserId, request.WindowId), cancellationToken);
        if (view.IsFailure)
        {
            return Result.Failure<CompiledReportFile>(view.Error);
        }

        var report = view.Value;
        var branding = await brandingProvider.GetAsync(cancellationToken, includeLogo: true);
        return request.Format == CompiledReportFormat.Word
            ? Result.Success(new CompiledReportFile(
                renderer.RenderWord(report, branding), "application/vnd.openxmlformats-officedocument.wordprocessingml.document", $"{report.ReferenceNumber}.docx"))
            : Result.Success(new CompiledReportFile(renderer.RenderPdf(report, branding), "application/pdf", $"{report.ReferenceNumber}.pdf"));
    }
}

/// <summary>FR-REV-006's narrative highlights: the Secretariat's own text for the report. Saving
/// an empty narrative clears it.</summary>
public sealed record UpdateCompiledNarrativeCommand(Guid UserId, Guid WindowId, string Narrative) : IRequest<Result>;

public sealed class UpdateCompiledNarrativeCommandValidator : AbstractValidator<UpdateCompiledNarrativeCommand>
{
    public UpdateCompiledNarrativeCommandValidator()
    {
        RuleFor(x => x.WindowId).NotEmpty();
        RuleFor(x => x.Narrative).NotNull().MaximumLength(CompiledReport.MaxNarrativeLength);
    }
}

public sealed class UpdateCompiledNarrativeCommandHandler(IAppDbContext db, IAccessPolicy accessPolicy, IClock clock, IAuditLog auditLog)
    : IRequestHandler<UpdateCompiledNarrativeCommand, Result>
{
    public async ValueTask<Result> Handle(UpdateCompiledNarrativeCommand request, CancellationToken cancellationToken)
    {
        var grant = await CompiledReportAccess.ResolveAsync(accessPolicy, request.UserId, cancellationToken);
        if (!grant.CanEdit)
        {
            return Result.Failure(Error.Forbidden("FORBIDDEN", "Only the Secretariat can edit the compiled report."));
        }

        if (!await db.SubmissionWindows.AnyAsync(w => w.Id == request.WindowId, cancellationToken))
        {
            return Result.Failure(Error.NotFound("WINDOW_NOT_FOUND", "That window doesn't exist."));
        }

        var compiled = await db.CompiledReports.FirstOrDefaultAsync(c => c.WindowId == request.WindowId, cancellationToken);
        if (compiled is null)
        {
            compiled = CompiledReport.Create(Guid.CreateVersion7(), request.WindowId);
            db.CompiledReports.Add(compiled);
        }

        var before = compiled.Narrative;
        compiled.UpdateNarrative(request.Narrative.Trim(), request.UserId, clock.UtcNow);
        auditLog.Add(new AuditEntry(
            AuditActions.CompiledReportEdited, request.UserId, "compiled_report", request.WindowId.ToString(),
            $"Edited the summary of {await AuditDescriptions.WindowAsync(db, request.WindowId, cancellationToken)}",
            Before: new { Summary = before }, After: new { Summary = compiled.Narrative }));
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
