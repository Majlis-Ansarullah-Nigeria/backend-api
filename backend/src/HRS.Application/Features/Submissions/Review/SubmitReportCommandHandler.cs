using System.Globalization;
using System.Text.Json;
using HRS.Application.Common.Interfaces;
using HRS.Application.Common.Options;
using HRS.Application.Features.Admin.Webhooks;
using HRS.Application.Features.Audit;
using HRS.Application.Features.Notifications;
using HRS.Application.Features.Submissions.ReportInstances;
using HRS.Application.Features.Templates.TemplateVersions;
using HRS.Domain.Common;
using HRS.Domain.Notifications;
using HRS.Domain.Submissions;
using HRS.Domain.Templates;
using HRS.Domain.Webhooks;
using HRS.Domain.Windows;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HRS.Application.Features.Submissions.Review;

public sealed class SubmitReportCommandHandler(
    IAppDbContext db, IAccessPolicy accessPolicy, IClock clock, IPdfRenderer pdfRenderer, IEmailSender emailSender,
    IIdentityService identityService, IOptions<AppOptions> appOptions, IAuditLog auditLog, IOrganizationBrandingProvider brandingProvider)
    : IRequestHandler<SubmitReportCommand, Result<SubmitReportResult>>
{
    public async ValueTask<Result<SubmitReportResult>> Handle(SubmitReportCommand request, CancellationToken cancellationToken)
    {
        if (!await accessPolicy.CanSubmitReportForUnitAsync(request.UserId, request.UnitId, cancellationToken))
        {
            return Result.Failure<SubmitReportResult>(
                Error.Forbidden("FORBIDDEN", "You don't have permission to submit this unit's reports."));
        }

        if (request.IdempotencyKey is { Length: > 0 } key)
        {
            var cached = await db.IdempotencyRecords.AsNoTracking()
                .FirstOrDefaultAsync(r => r.UserId == request.UserId && r.Key == key, cancellationToken);
            if (cached is not null)
            {
                return JsonSerializer.Deserialize<SubmitReportResult>(cached.ResponseJson, JsonSerializerOptions.Web)!;
            }
        }

        // Same dual-mode resolution as Get/SaveReportDraft/Presign.
        var instance = await db.ReportInstances.FirstOrDefaultAsync(i => i.Id == request.ReportId && i.UnitId == request.UnitId, cancellationToken);
        instance ??= await db.ReportInstances.FirstOrDefaultAsync(i => i.WindowId == request.ReportId && i.UnitId == request.UnitId, cancellationToken);

        var window = await db.SubmissionWindows.AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == (instance != null ? instance.WindowId : request.ReportId), cancellationToken);
        if (window is null)
        {
            return Result.Failure<SubmitReportResult>(Error.NotFound("REPORT_NOT_FOUND", "Report not found."));
        }

        var reportType = await db.ReportTypes.AsNoTracking().FirstOrDefaultAsync(r => r.Id == window.ReportTypeId, cancellationToken);
        if (reportType is null)
        {
            return Result.Failure<SubmitReportResult>(Error.NotFound("REPORT_NOT_FOUND", "Report not found."));
        }

        var unitRow = await db.Units.AsNoTracking()
            .Where(u => u.Id == request.UnitId)
            .Join(db.UnitLevels.AsNoTracking(), u => u.LevelId, l => l.Id, (u, l) => new { u.Code, u.Name, u.ParentId, LevelCode = l.Code })
            .FirstOrDefaultAsync(cancellationToken);
        if (unitRow is null)
        {
            return Result.Failure<SubmitReportResult>(Error.NotFound("REPORT_NOT_FOUND", "Report not found."));
        }

        var now = clock.UtcNow;
        var editError = ReportEditability.Check(window, instance, unitRow.LevelCode, request.UnitId, now);
        if (editError is not null)
        {
            return Result.Failure<SubmitReportResult>(editError);
        }

        if (!request.Confirmed)
        {
            return Result.Failure<SubmitReportResult>(
                Error.Validation("DECLARATION_REQUIRED", "Confirm the declaration before submitting."));
        }

        if (request.IsNil && !reportType.AllowNilReport)
        {
            return Result.Failure<SubmitReportResult>(
                Error.Validation("NIL_REPORT_NOT_ALLOWED", "This report type doesn't accept a nil submission."));
        }

        if (instance is null && !request.IsNil)
        {
            return Result.Failure<SubmitReportResult>(Error.Conflict("NOTHING_TO_SUBMIT", "Save a draft before submitting."));
        }

        TemplateVersion templateVersion;
        if (instance is not null)
        {
            templateVersion = await db.TemplateVersions.AsNoTracking().FirstAsync(v => v.Id == instance.TemplateVersionId, cancellationToken);
        }
        else
        {
            // Only reachable when IsNil — a one-click "no activity" submission on a report
            // nobody ever saved a draft for (FR-SUB-012), the same lazy-creation pattern Save
            // and Presign already use.
            var published = await db.TemplateVersions.AsNoTracking()
                .FirstOrDefaultAsync(v => v.ReportTypeId == reportType.Id && v.Status == TemplateVersionStatus.Published, cancellationToken);
            if (published is null)
            {
                return Result.Failure<SubmitReportResult>(
                    Error.Conflict("NO_PUBLISHED_TEMPLATE", "This report type has no published template yet."));
            }

            templateVersion = published;
            instance = ReportInstance.Create(Guid.CreateVersion7(), window.Id, request.UnitId, published.Id, now);
            instance.Save("{}");
            db.ReportInstances.Add(instance);
        }

        var schema = JsonSerializer.Deserialize<TemplateSchema>(templateVersion.SchemaJson, JsonSerializerOptions.Web)!;
        var answers = JsonSerializer.Deserialize<Dictionary<string, object?>>(instance.AnswersJson, JsonSerializerOptions.Web) ?? [];

        if (!request.IsNil)
        {
            var missingFields = ReportCompleteness.FindMissingRequiredFields(schema, unitRow.LevelCode, answers);
            if (missingFields.Count > 0)
            {
                var labels = string.Join(", ", missingFields.Select(f => f.Label));
                return Result.Failure<SubmitReportResult>(
                    Error.Validation("INCOMPLETE_REQUIRED_FIELDS", $"These required fields are still empty: {labels}."));
            }
        }

        var isLate = window.StatusFor(unitRow.LevelCode, now, request.UnitId) == WindowLevelStatus.Late;
        var referenceNumber = instance.ReferenceNumber
            ?? await FreeReferenceAsync(ReportReferenceNumber.Generate(reportType.Code, reportType.Category, window.PeriodLabel, now, unitRow.Code), cancellationToken);

        instance.Submit(isLate, referenceNumber, request.IsNil, now, request.UserId);
        db.ReportRevisions.Add(instance.RecordSubmission(request.UserId, now));
        await WebhookEnqueuer.EnqueueAsync(
            db, WebhookEvent.ReportSubmitted,
            () => WebhookPayloads.ReportAsync(db, instance, WebhookEvent.ReportSubmitted, appOptions.Value.PublicUrl, now, cancellationToken),
            now, cancellationToken);
        auditLog.Add(new AuditEntry(
            AuditActions.ReportSubmitted, request.UserId, "report", instance.Id.ToString(),
            $"{reportType.Name} · {window.PeriodLabel} · {unitRow.Name}{(isLate ? " (late)" : string.Empty)}", request.UnitId,
            After: new { Reference = instance.ReferenceNumber, IsLate = isLate, request.IsNil }));

        // FR-NOT-002's "child submitted (to observer)" — only when this report type actually has
        // an observer scope (FR-OBS's ObserverDepth>=1 gate, same check Nudge/PrivateNotes use)
        // and this unit has a parent to notify (Zone-level units route straight to the
        // Secretariat with no observer, per hrs-domain's routing table).
        if (reportType.ObserverDepth >= 1 && unitRow.ParentId is { } parentUnitId)
        {
            var placeholders = new Dictionary<string, string>
            {
                ["unitName"] = unitRow.Name,
                ["reportTypeName"] = reportType.Name,
                ["periodLabel"] = window.PeriodLabel,
                ["link"] = $"{appOptions.Value.PublicUrl.TrimEnd('/')}/units/{request.UnitId}/reports/{instance.Id}",
            };
            await OutboxWriter.EnqueueForUnitHeadsAsync(db, parentUnitId, NotificationEventType.ChildSubmitted, placeholders, now, cancellationToken);
        }

        var result = new SubmitReportResult(instance.Id, instance.ReferenceNumber!, now, isLate);

        if (request.IdempotencyKey is { Length: > 0 } newKey)
        {
            db.IdempotencyRecords.Add(IdempotencyRecord.Create(
                Guid.CreateVersion7(), request.UserId, newKey, JsonSerializer.Serialize(result, JsonSerializerOptions.Web), now));
        }

        await db.SaveChangesAsync(cancellationToken);

        await SendReceiptEmailAsync(
            request.UserId, instance, schema, reportType.Name, window.PeriodLabel, unitRow.Name, (int)unitRow.LevelCode, referenceNumber, now, cancellationToken);

        return result;
    }

    /// <summary>Renders the PDF and hands it to IEmailSender — the actual SMTP send still
    /// happens off the request thread (BackgroundEmailDispatcher), only the (fast, CPU-bound)
    /// rendering runs here, after the submission has already been durably saved.</summary>
    private async Task SendReceiptEmailAsync(
        Guid userId, ReportInstance instance, TemplateSchema schema, string reportTypeName, string periodLabel,
        string unitName, int unitLevel, string referenceNumber, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var user = await identityService.GetUserAsync(userId, cancellationToken);
        if (user is null)
        {
            return;
        }

        var receiptData = await ReportReceiptBuilder.BuildAsync(
            db, instance, schema, reportTypeName, periodLabel, unitName, unitLevel, referenceNumber, now, cancellationToken);
        var branding = await brandingProvider.GetAsync(cancellationToken, includeLogo: true);
        var pdfBytes = pdfRenderer.RenderReportReceipt(receiptData, branding);

        var submittedAtText = branding.DateTime(now);
        await emailSender.QueueAsync(
            new EmailMessage(
                user.Email,
                $"{reportTypeName} submitted — {referenceNumber}",
                $"The National Secretariat has your {periodLabel} report.\n\nReference: {referenceNumber}\nSubmitted: {submittedAtText}\n\nA PDF copy is attached.",
                new EmailAttachment($"{referenceNumber}.pdf", "application/pdf", pdfBytes)),
            cancellationToken);
    }

    /// <summary>The same type, period and unit can recur in a second window (a re-run period); the
    /// unique reference then takes the next free suffix instead of failing the submission.</summary>
    private async Task<string> FreeReferenceAsync(string reference, CancellationToken cancellationToken)
    {
        var taken = (await db.ReportInstances.AsNoTracking()
                .Where(i => i.ReferenceNumber != null && (i.ReferenceNumber == reference || i.ReferenceNumber.StartsWith(reference + "-")))
                .Select(i => i.ReferenceNumber!)
                .ToListAsync(cancellationToken))
            .ToHashSet();

        for (var attempt = 1; ; attempt++)
        {
            var candidate = ReportReferenceNumber.WithSuffix(reference, attempt);
            if (!taken.Contains(candidate))
            {
                return candidate;
            }
        }
    }
}
