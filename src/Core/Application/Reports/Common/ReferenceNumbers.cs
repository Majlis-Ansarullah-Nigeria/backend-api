using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Domain.Entities.Reports;
using ManagementApi.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ManagementApi.Application.Reports.Common;

/// <summary>Issues the reference number for a report on its first submission.</summary>
public static class ReferenceNumbers
{
    public static async Task<string> NextAsync(
        IApplicationDbContext context,
        ReportSubmission report,
        SubmissionWindow window,
        CancellationToken cancellationToken)
    {
        var template = await context.ReportTemplates
            .Include(t => t.ReportTypeEntity)
            .FirstAsync(t => t.Id == report.ReportTemplateId, cancellationToken);

        var type = template.ReportTypeEntity;
        var typeCode = type?.Code ?? ReportReferenceNumber.FallbackTypeCode(template.ReportType);
        var frequency = type?.Frequency ?? GuessFrequency(template.ReportType);
        var period = ReportReferenceNumber.Period(frequency, window.ReferencePeriod);

        var unitParts = new List<string>();
        if (report.ZoneId is { } zoneId)
        {
            var code = await context.Zones.Where(z => z.Id == zoneId).Select(z => z.Code).FirstOrDefaultAsync(cancellationToken);
            unitParts.Add(ReportReferenceNumber.UnitPart('Z', code, zoneId));
        }

        if (report.DilaId is { } dilaId)
        {
            var code = await context.Dilas.Where(d => d.Id == dilaId).Select(d => d.Code).FirstOrDefaultAsync(cancellationToken);
            unitParts.Add(ReportReferenceNumber.UnitPart('D', code, dilaId));
        }

        if (report.OrganizationLevel == OrganizationLevel.Muqam && report.MuqamId is { } muqamId)
        {
            var code = await context.Muqams.Where(m => m.Id == muqamId).Select(m => m.Code).FirstOrDefaultAsync(cancellationToken);
            unitParts.Add(ReportReferenceNumber.UnitPart('M', code, muqamId));
        }

        if (unitParts.Count == 0)
        {
            unitParts.Add("NAT");
        }

        var baseReference = ReportReferenceNumber.Build(typeCode, period, unitParts.ToArray());

        // The same type, period and unit can recur in a second window: add -2, -3 … instead of failing.
        var taken = await context.ReportSubmissions
            .Where(s => s.ReferenceNumber != null && s.ReferenceNumber.StartsWith(baseReference))
            .Select(s => s.ReferenceNumber!)
            .ToListAsync(cancellationToken);

        for (var attempt = 1; ; attempt++)
        {
            var candidate = ReportReferenceNumber.WithSuffix(baseReference, attempt);
            if (!taken.Contains(candidate))
            {
                return candidate;
            }
        }
    }

    private static ReportFrequency GuessFrequency(string? templateReportType)
    {
        var text = templateReportType?.ToLowerInvariant() ?? string.Empty;
        if (text.Contains("quarter")) return ReportFrequency.Quarterly;
        if (text.Contains("annual") || text.Contains("year")) return ReportFrequency.Annual;
        return ReportFrequency.Monthly;
    }
}
