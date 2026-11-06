using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Domain.Entities.Reports;
using ManagementApi.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ManagementApi.Application.Reports.Common;

/// <summary>
/// Loads the current user's own unit report for a window, creating the draft on first save.
/// There is at most one report per unit per window (also enforced by a unique index).
/// </summary>
public static class OwnReport
{
    /// <param name="IsLate">Whether a submission now would be past this unit's due date.</param>
    public record Loaded(ReportSubmission Report, SubmissionWindow Window, bool IsNew, bool IsLate);

    public static async Task<(Loaded? Loaded, string? Error)> LoadOrCreateAsync(
        IApplicationDbContext context,
        ICurrentUserService user,
        Guid reportTemplateId,
        Guid submissionWindowId,
        string responseData,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var window = await context.SubmissionWindows
            .Include(w => w.Extensions)
            .FirstOrDefaultAsync(w => w.Id == submissionWindowId, cancellationToken);

        if (window is null)
        {
            return (null, "Submission window not found");
        }

        if (window.ReportTemplateId != reportTemplateId)
        {
            return (null, "This report template does not belong to the selected submission window");
        }

        var template = await context.ReportTemplates
            .FirstOrDefaultAsync(t => t.Id == reportTemplateId, cancellationToken);

        if (template is null)
        {
            return (null, "Report template not found");
        }

        var (unit, unitError) = await ReportingScope.ResolveOwnUnitAsync(user, context, cancellationToken);
        if (unit is null)
        {
            return (null, unitError);
        }

        var report = await context.ReportSubmissions
            .FirstOrDefaultAsync(s =>
                s.SubmissionWindowId == window.Id &&
                s.OrganizationLevel == unit.Level &&
                s.UnitId == unit.UnitId,
                cancellationToken);

        // Returned reports may be revised after the window closes, until their revision deadline.
        var isRevision = report?.Status == SubmissionStatus.Returned;
        if (!isRevision && !window.IsOpenFor(nowUtc, unit.Level, unit.UnitId))
        {
            return (null, nowUtc < window.StartDate
                ? "WINDOW_NOT_OPEN: This submission window has not opened yet"
                : "WINDOW_CLOSED: This submission window is closed");
        }

        var isLate = window.IsLateFor(nowUtc, unit.Level, unit.UnitId);

        if (report is not null)
        {
            return (new Loaded(report, window, false, isLate), null);
        }

        report = new ReportSubmission(
            reportTemplateId,
            window.Id,
            unit.Level,
            unit.UnitId,
            unit.MuqamId,
            unit.DilaId,
            unit.ZoneId,
            user.UserId,
            user.ChandaNo ?? "UNKNOWN",
            user.UserName ?? "Unknown User",
            user.Email,
            responseData);

        context.ReportSubmissions.Add(report);
        return (new Loaded(report, window, true, isLate), null);
    }
}
