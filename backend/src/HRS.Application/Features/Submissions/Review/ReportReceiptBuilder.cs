using System.Text.Json;
using HRS.Application.Common.Interfaces;
using HRS.Application.Features.Templates.TemplateVersions;
using HRS.Domain.Submissions;
using HRS.Domain.Templates;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Submissions.Review;

/// <summary>Builds the receipt PDF's content (FR-SUB-010) — shared by the just-submitted receipt
/// (SubmitReportCommand, which already has everything in hand and a real reference number) and
/// the on-demand "download draft PDF" / "download PDF" endpoints (GetReportReceiptPdfQuery,
/// which re-fetches and, pre-submit, has no reference number yet).</summary>
internal static class ReportReceiptBuilder
{
    public static async Task<ReportReceiptData> BuildAsync(
        IAppDbContext db,
        ReportInstance instance,
        TemplateSchema schema,
        string reportTypeName,
        string periodLabel,
        string unitName,
        int unitLevel,
        string? referenceNumber,
        DateTimeOffset generatedAt,
        CancellationToken cancellationToken)
    {
        var answers = JsonSerializer.Deserialize<Dictionary<string, object?>>(instance.AnswersJson, JsonSerializerOptions.Web) ?? [];

        var attachments = await db.ReportAttachments.AsNoTracking()
            .Where(a => a.ReportInstanceId == instance.Id && a.Status == ReportAttachmentStatus.Clean)
            .ToListAsync(cancellationToken);
        var attachmentsByField = attachments
            .GroupBy(a => a.FieldKey)
            .ToDictionary(g => g.Key, IReadOnlyList<string> (g) => [.. g.Select(a => a.FileName)]);

        var sections = schema.Sections
            .Select(section => new ReportReceiptSection(
                section.Title,
                [.. FieldVisibility.VisibleFields(section.Fields, (Domain.Organization.UnitLevelCode)unitLevel, answers)
                    .Where(f => f.Type != TemplateFieldType.Computed)
                    .Select(f => new ReportReceiptField(f.Label, AnswerDisplay.Format(f, answers.GetValueOrDefault(f.Key), attachmentsByField)))]))
            .Where(s => s.Fields.Count > 0)
            .ToList();

        return new ReportReceiptData(reportTypeName, periodLabel, unitName, referenceNumber, generatedAt, sections);
    }
}
