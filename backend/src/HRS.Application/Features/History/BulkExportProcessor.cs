using System.IO.Compression;
using System.Text.Json;
using HRS.Application.Common.Interfaces;
using HRS.Application.Features.Notifications;
using HRS.Application.Features.Submissions.MyReports;
using HRS.Application.Features.Submissions.Review;
using HRS.Application.Features.Templates.TemplateVersions;
using HRS.Domain.History;
using HRS.Domain.Notifications;
using HRS.Domain.Submissions;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.History;

/// <summary>
/// FR-HIS-004's actual background work — pure, directly testable logic, same split as
/// OutboxDispatcher/WindowJobRunner. The Hangfire-invokable adapter (Infrastructure, so both the
/// API's enqueuer and the Worker's processor share the same compiled type) just calls this.
/// </summary>
public static class BulkExportProcessor
{
    private sealed record ResolvedReport(HistoryReportPdfData PdfData, BulkExportReportRow WorkbookRow);

    public static async Task RunAsync(
        IAppDbContext db, IFileStorage fileStorage, IPdfRenderer pdfRenderer, IExcelRenderer excelRenderer, IClock clock,
        string publicUrl, Guid bulkExportId, CancellationToken cancellationToken, OrganizationBranding? branding = null)
    {
        var export = await db.BulkExports.FirstOrDefaultAsync(e => e.Id == bulkExportId, cancellationToken);
        if (export is null || export.Status != BulkExportStatus.Pending)
        {
            return;
        }

        export.MarkProcessing();
        await db.SaveChangesAsync(cancellationToken);

        try
        {
            var reports = export.ScopeKind == BulkExportScopeKind.InstanceIds
                ? await ResolveFromInstanceIdsAsync(db, export, publicUrl, clock, cancellationToken)
                : await ResolveFromOrganizationFilterAsync(db, export, publicUrl, clock, cancellationToken);

            if (reports.Count == 0)
            {
                export.MarkFailed("None of the selected reports could be found.", clock.UtcNow);
                await db.SaveChangesAsync(cancellationToken);
                return;
            }

            var key = $"exports/{export.Id}.{(export.Format == BulkExportFormat.ZipOfPdfs ? "zip" : "xlsx")}";
            var contentType = export.Format == BulkExportFormat.ZipOfPdfs
                ? "application/zip"
                : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

            var fileBytes = export.Format == BulkExportFormat.ZipOfPdfs
                ? BuildZipOfPdfs(pdfRenderer, reports, branding ?? OrganizationBranding.Default)
                : excelRenderer.RenderBulkWorkbook([.. reports.Select(r => r.WorkbookRow)], branding ?? OrganizationBranding.Default);

            await fileStorage.UploadAsync(key, fileBytes, contentType, cancellationToken);

            var now = clock.UtcNow;
            export.MarkReady(key, reports.Count, now);

            OutboxWriter.EnqueueForUser(db, export.RequestedByUserId, NotificationEventType.ExportReady, new Dictionary<string, string>
            {
                ["reportCount"] = reports.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["link"] = $"{publicUrl.TrimEnd('/')}/exports/{export.Id}",
            }, now);

            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            export.MarkFailed(ex.Message, clock.UtcNow);
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static async Task<List<ResolvedReport>> ResolveFromInstanceIdsAsync(
        IAppDbContext db, BulkExport export, string publicUrl, IClock clock, CancellationToken cancellationToken)
    {
        var scope = JsonSerializer.Deserialize<InstanceIdsScope>(export.ScopeJson, JsonSerializerOptions.Web) ?? new InstanceIdsScope([]);

        var reports = new List<ResolvedReport>();
        foreach (var instanceId in scope.InstanceIds)
        {
            var resolved = await HistoryReportResolver.ResolveAsync(db, export.RequestedByUserId, instanceId, clock, cancellationToken);
            if (resolved.IsFailure)
            {
                // Best-effort: access may have changed between selecting rows and the job
                // running. Skip silently rather than failing the whole export over one row.
                continue;
            }

            var r = resolved.Value;
            var onlineRecordUrl = HistoryOnlineRecordUrl.Build(publicUrl, r.UnitId, r.Instance.Id, r.IsObserved);
            reports.Add(await BuildResolvedReportAsync(
                db, r.Instance, r.Schema, r.ReportType.Name, r.Window.PeriodLabel, r.UnitName, (int)r.UnitLevelCode,
                r.IsObserved, onlineRecordUrl, clock, cancellationToken));
        }

        return reports;
    }

    private static async Task<List<ResolvedReport>> ResolveFromOrganizationFilterAsync(
        IAppDbContext db, BulkExport export, string publicUrl, IClock clock, CancellationToken cancellationToken)
    {
        var filter = JsonSerializer.Deserialize<OrganizationFilterScope>(export.ScopeJson, JsonSerializerOptions.Web)
            ?? new OrganizationFilterScope(null, null, null, null);

        var reportTypes = await db.ReportTypes.AsNoTracking()
            .Where(r => filter.ReportTypeId == null || r.Id == filter.ReportTypeId)
            .ToListAsync(cancellationToken);
        var reportTypeById = reportTypes.ToDictionary(r => r.Id);

        var windows = await db.SubmissionWindows.AsNoTracking()
            .Where(w => reportTypeById.Keys.Contains(w.ReportTypeId))
            .ToListAsync(cancellationToken);

        DateTimeOffset? fromOpensAt = filter.FromWindowId is { } fromId ? windows.Find(w => w.Id == fromId)?.OpensAt : null;
        DateTimeOffset? toOpensAt = filter.ToWindowId is { } toId ? windows.Find(w => w.Id == toId)?.OpensAt : null;

        var windowsInScope = windows
            .Where(w => (fromOpensAt is null || w.OpensAt >= fromOpensAt) && (toOpensAt is null || w.OpensAt <= toOpensAt))
            .ToList();
        var windowById = windowsInScope.ToDictionary(w => w.Id);

        var instances = await db.ReportInstances.AsNoTracking()
            .Where(i => windowById.Keys.Contains(i.WindowId))
            .ToListAsync(cancellationToken);

        var unitIds = instances.Select(i => i.UnitId).Distinct().ToList();
        var units = await db.Units.AsNoTracking()
            .Where(u => unitIds.Contains(u.Id))
            .Join(db.UnitLevels.AsNoTracking(), u => u.LevelId, l => l.Id, (u, l) => new { u.Id, u.Name, LevelCode = l.Code })
            .ToListAsync(cancellationToken);
        var unitById = units.ToDictionary(u => u.Id);

        var now = clock.UtcNow;
        var reports = new List<ResolvedReport>();
        foreach (var instance in instances)
        {
            if (!windowById.TryGetValue(instance.WindowId, out var window)
                || !reportTypeById.TryGetValue(window.ReportTypeId, out var reportType)
                || !unitById.TryGetValue(instance.UnitId, out var unit))
            {
                continue;
            }

            var status = MyReportStatusCalculator.Determine(window, instance, unit.LevelCode, instance.UnitId, now);
            if (!HistoryStatuses.All.Contains(status))
            {
                continue;
            }

            if (filter.Status is { } requestedStatus && (int)status != requestedStatus)
            {
                continue;
            }

            var templateVersion = await db.TemplateVersions.AsNoTracking()
                .FirstOrDefaultAsync(v => v.Id == instance.TemplateVersionId, cancellationToken);
            if (templateVersion is null)
            {
                continue;
            }

            var schema = JsonSerializer.Deserialize<TemplateSchema>(templateVersion.SchemaJson, JsonSerializerOptions.Web)!;

            // The Secretariat can act on every report (FR-AC-006's masking is an observer-only
            // rule) — never masked here. No dedicated "Secretariat view" route exists yet, so
            // this reuses the submitter's own edit route — the best available stable link.
            var onlineRecordUrl = HistoryOnlineRecordUrl.Build(publicUrl, unit.Id, instance.Id, isObserved: false);
            reports.Add(await BuildResolvedReportAsync(
                db, instance, schema, reportType.Name, window.PeriodLabel, unit.Name, (int)unit.LevelCode,
                isObserved: false, onlineRecordUrl, clock, cancellationToken));
        }

        return reports;
    }

    private static async Task<ResolvedReport> BuildResolvedReportAsync(
        IAppDbContext db, ReportInstance instance, TemplateSchema schema, string reportTypeName, string periodLabel,
        string unitName, int unitLevel, bool isObserved, string onlineRecordUrl, IClock clock, CancellationToken cancellationToken)
    {
        var receipt = await ReportReceiptBuilder.BuildAsync(
            db, instance, schema, reportTypeName, periodLabel, unitName, unitLevel,
            instance.ReferenceNumber, instance.SubmittedAt ?? clock.UtcNow, cancellationToken, hideConfidentialValues: isObserved);

        var pdfData = new HistoryReportPdfData(
            receipt.ReportTypeName, receipt.PeriodLabel, receipt.UnitName, receipt.ReferenceNumber, receipt.GeneratedAt,
            onlineRecordUrl, receipt.Sections);

        var answersByFieldKey = receipt.Sections
            .SelectMany(s => s.Fields)
            .ToDictionary(f => f.FieldKey, f => (f.Label, f.Value));

        var workbookRow = new BulkExportReportRow(
            receipt.ReportTypeName, receipt.UnitName, receipt.PeriodLabel, receipt.ReferenceNumber, instance.SubmittedAt,
            instance.Status.ToString(), answersByFieldKey);

        return new ResolvedReport(pdfData, workbookRow);
    }

    private static byte[] BuildZipOfPdfs(IPdfRenderer pdfRenderer, List<ResolvedReport> reports, OrganizationBranding branding)
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var usedNames = new HashSet<string>();
            foreach (var report in reports)
            {
                var bytes = pdfRenderer.RenderHistoryReport(report.PdfData, branding);
                var baseName = (report.PdfData.ReferenceNumber ?? $"{report.PdfData.ReportTypeName}-{report.PdfData.UnitName}").Replace('/', '-');
                var fileName = $"{baseName}.pdf";
                var suffix = 1;
                while (!usedNames.Add(fileName))
                {
                    fileName = $"{baseName}-{++suffix}.pdf";
                }

                var entry = zip.CreateEntry(fileName, CompressionLevel.Optimal);
                using var entryStream = entry.Open();
                entryStream.Write(bytes);
            }
        }

        return stream.ToArray();
    }
}
