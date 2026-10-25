using System.Text.Json;
using HRS.Application.Common.Interfaces;
using HRS.Application.Features.Submissions.Review;
using HRS.Application.Features.Templates.TemplateVersions;
using HRS.Domain.Submissions;
using HRS.Domain.Templates;
using HRS.Domain.Webhooks;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Admin.Webhooks;

/// <summary>
/// FR-ADM-003's payloads. A report event carries the report's metadata and its answers — each with
/// its section, key, label, type, raw value and display text — but never confidential fields
/// (FR-AC-006), computed fields or attachments.
/// </summary>
public static class WebhookPayloads
{
    public static async Task<string> ReportAsync(
        IAppDbContext db, ReportInstance instance, WebhookEvent webhookEvent, string publicUrl, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var window = await db.SubmissionWindows.AsNoTracking().FirstAsync(w => w.Id == instance.WindowId, cancellationToken);
        var reportType = await db.ReportTypes.AsNoTracking().FirstAsync(r => r.Id == window.ReportTypeId, cancellationToken);
        var unit = await db.Units.AsNoTracking()
            .Where(u => u.Id == instance.UnitId)
            .Join(db.UnitLevels.AsNoTracking(), u => u.LevelId, l => l.Id, (u, l) => new { u.Id, u.Code, u.Name, Level = l.Code })
            .FirstAsync(cancellationToken);
        var schemaJson = await db.TemplateVersions.AsNoTracking()
            .Where(v => v.Id == instance.TemplateVersionId).Select(v => v.SchemaJson).FirstAsync(cancellationToken);
        var schema = JsonSerializer.Deserialize<TemplateSchema>(schemaJson, JsonSerializerOptions.Web)!;
        var answers = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(instance.AnswersJson, JsonSerializerOptions.Web) ?? [];
        var noAttachments = new Dictionary<string, IReadOnlyList<string>>();

        var answerList = schema.Sections
            .SelectMany(section => section.Fields.Select(field => (Section: section, Field: field)))
            .Where(x => !x.Field.Confidential
                && x.Field.Type is not (TemplateFieldType.Computed or TemplateFieldType.FileUpload)
                && FieldVisibility.IsVisibleForLevel(x.Field, unit.Level)
                && answers.ContainsKey(x.Field.Key))
            .Select(x => new
            {
                section = x.Section.Title,
                key = x.Field.Key,
                label = x.Field.Label,
                type = x.Field.Type.ToString(),
                value = answers[x.Field.Key],
                display = AnswerDisplay.Format(x.Field, answers[x.Field.Key], noAttachments),
            })
            .ToList();

        var payload = new
        {
            @event = WebhookEventNames.Name(webhookEvent),
            occurredAt = now,
            report = new
            {
                id = instance.Id,
                referenceNumber = instance.ReferenceNumber,
                status = instance.Status.ToString(),
                isLate = instance.IsLate,
                isNil = instance.IsNil,
                submittedAt = instance.SubmittedAt,
                acknowledgedAt = instance.AcknowledgedAt,
                period = window.PeriodLabel,
                windowId = window.Id,
                reportType = new { id = reportType.Id, name = reportType.Name },
                unit = new { id = unit.Id, code = unit.Code, name = unit.Name, level = unit.Level.ToString() },
                link = $"{publicUrl.TrimEnd('/')}/history/{instance.Id}/versions",
            },
            answers = answerList,
        };

        return JsonSerializer.Serialize(payload, JsonSerializerOptions.Web);
    }

    public static string Ping(DateTimeOffset now) =>
        JsonSerializer.Serialize(new { @event = WebhookEventNames.Name(WebhookEvent.Ping), occurredAt = now }, JsonSerializerOptions.Web);
}
