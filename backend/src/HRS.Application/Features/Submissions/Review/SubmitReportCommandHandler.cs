using System.Globalization;
using System.Text.Json;
using HRS.Application.Common.Interfaces;
using HRS.Application.Features.Submissions.ReportInstances;
using HRS.Application.Features.Templates.TemplateVersions;
using HRS.Domain.Common;
using HRS.Domain.Submissions;
using HRS.Domain.Windows;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Submissions.Review;

public sealed class SubmitReportCommandHandler(
    IAppDbContext db, IAccessPolicy accessPolicy, IClock clock, IPdfRenderer pdfRenderer, IEmailSender emailSender, IIdentityService identityService)
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

        if (instance is null)
        {
            return Result.Failure<SubmitReportResult>(Error.Conflict("NOTHING_TO_SUBMIT", "Save a draft before submitting."));
        }

        var window = await db.SubmissionWindows.AsNoTracking().FirstOrDefaultAsync(w => w.Id == instance.WindowId, cancellationToken);
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
            .Join(db.UnitLevels.AsNoTracking(), u => u.LevelId, l => l.Id, (u, l) => new { u.Code, u.Name, LevelCode = l.Code })
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

        var templateVersion = await db.TemplateVersions.AsNoTracking().FirstAsync(v => v.Id == instance.TemplateVersionId, cancellationToken);
        var schema = JsonSerializer.Deserialize<TemplateSchema>(templateVersion.SchemaJson, JsonSerializerOptions.Web)!;
        var answers = JsonSerializer.Deserialize<Dictionary<string, object?>>(instance.AnswersJson, JsonSerializerOptions.Web) ?? [];

        var missingFields = ReportCompleteness.FindMissingRequiredFields(schema, unitRow.LevelCode, answers);
        if (missingFields.Count > 0)
        {
            var labels = string.Join(", ", missingFields.Select(f => f.Label));
            return Result.Failure<SubmitReportResult>(
                Error.Validation("INCOMPLETE_REQUIRED_FIELDS", $"These required fields are still empty: {labels}."));
        }

        var isLate = window.StatusFor(unitRow.LevelCode, now, request.UnitId) == WindowLevelStatus.Late;
        var referenceNumber = instance.ReferenceNumber
            ?? ReportReferenceNumber.Generate(reportType.Category, window.PeriodLabel, now, unitRow.Code);

        instance.Submit(isLate, referenceNumber, now);

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
        var pdfBytes = pdfRenderer.RenderReportReceipt(receiptData);

        var submittedAtText = now.ToString("d MMM yyyy, HH:mm", CultureInfo.InvariantCulture);
        await emailSender.QueueAsync(
            new EmailMessage(
                user.Email,
                $"{reportTypeName} submitted — {referenceNumber}",
                $"The National Secretariat has your {periodLabel} report.\n\nReference: {referenceNumber}\nSubmitted: {submittedAtText}\n\nA PDF copy is attached.",
                new EmailAttachment($"{referenceNumber}.pdf", "application/pdf", pdfBytes)),
            cancellationToken);
    }
}
