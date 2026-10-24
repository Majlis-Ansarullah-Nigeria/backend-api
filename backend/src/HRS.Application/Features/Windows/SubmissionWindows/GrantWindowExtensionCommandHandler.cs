using System.Globalization;
using HRS.Application.Common.Interfaces;
using HRS.Application.Common.Options;
using HRS.Application.Features.Audit;
using HRS.Application.Features.Notifications;
using HRS.Domain.Common;
using HRS.Domain.Notifications;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HRS.Application.Features.Windows.SubmissionWindows;

public sealed class GrantWindowExtensionCommandHandler(
    IAppDbContext db, IAccessPolicy accessPolicy, IClock clock, IOptions<AppOptions> appOptions, IAuditLog auditLog,
    IOrganizationBrandingProvider brandingProvider)
    : IRequestHandler<GrantWindowExtensionCommand, Result<SubmissionWindowSummary>>
{
    public async ValueTask<Result<SubmissionWindowSummary>> Handle(GrantWindowExtensionCommand request, CancellationToken cancellationToken)
    {
        if (!await accessPolicy.IsWindowAdminAsync(request.UserId, cancellationToken))
        {
            return Result.Failure<SubmissionWindowSummary>(
                Error.Forbidden("FORBIDDEN", "Only the Secretariat can manage submission windows."));
        }

        var window = await db.SubmissionWindows.FirstOrDefaultAsync(
            w => w.Id == request.WindowId && w.ReportTypeId == request.ReportTypeId, cancellationToken);

        if (window is null)
        {
            return Result.Failure<SubmissionWindowSummary>(Error.NotFound("WINDOW_NOT_FOUND", "Submission window not found."));
        }

        if (window.TargetUnitIds.Count > 0 && request.UnitIds.Except(window.TargetUnitIds).Any())
        {
            return Result.Failure<SubmissionWindowSummary>(Error.Validation(
                "UNIT_NOT_TARGETED_BY_WINDOW", "This window doesn't target one or more of the given units."));
        }

        var dueDates = window.DueDates.Select(d => new WindowDueDate(d.Level, d.DueAt)).ToList();
        var targetUnitError = await TargetUnitValidator.ValidateAsync(db, request.UnitIds, dueDates, cancellationToken);
        if (targetUnitError is not null)
        {
            return Result.Failure<SubmissionWindowSummary>(targetUnitError);
        }

        var now = clock.UtcNow;
        var branding = await brandingProvider.GetAsync(cancellationToken);
        var before = WindowAudit.Snapshot(window);
        window.GrantExtension(request.UnitIds, request.NewDueAt, request.NewClosesAt, request.Reason, now);
        var unitNames = await db.Units.AsNoTracking().Where(u => request.UnitIds.Contains(u.Id)).Select(u => u.Name).ToListAsync(cancellationToken);
        auditLog.Add(new AuditEntry(
            AuditActions.WindowExtended, request.UserId, "window", window.Id.ToString(),
            $"{string.Join(", ", unitNames)} · due → {branding.Date(request.NewDueAt)}",
            request.UnitIds.Count == 1 ? request.UnitIds[0] : null,
            Before: new { before.DueDates, before.ClosesAt },
            After: new { Units = unitNames, DueAt = request.NewDueAt, ClosesAt = request.NewClosesAt, request.Reason }));

        var reportType = await db.ReportTypes.AsNoTracking().FirstAsync(r => r.Id == window.ReportTypeId, cancellationToken);
        var unitNamesById = await db.Units.AsNoTracking()
            .Where(u => request.UnitIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Name, cancellationToken);
        var publicUrl = appOptions.Value.PublicUrl.TrimEnd('/');

        foreach (var unitId in request.UnitIds)
        {
            var placeholders = new Dictionary<string, string>
            {
                ["unitName"] = unitNamesById.GetValueOrDefault(unitId, "Your unit"),
                ["reportTypeName"] = reportType.Name,
                ["periodLabel"] = window.PeriodLabel,
                ["dueDate"] = branding.Date(request.NewDueAt),
                ["reason"] = request.Reason,
                ["link"] = $"{publicUrl}/reports/{window.Id}/edit?unit={unitId}",
            };
            await OutboxWriter.EnqueueForUnitHeadsAsync(db, unitId, NotificationEventType.ExtensionGranted, placeholders, now, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);

        return SubmissionWindowSummary.FromEntity(window);
    }
}
