using System.Text.Json;
using HRS.Application.Common.Interfaces;
using HRS.Application.Features.Submissions.Review;
using HRS.Application.Features.Templates.TemplateVersions;
using HRS.Domain.Analytics;
using HRS.Domain.Submissions;
using HRS.Domain.Templates;
using HRS.Domain.Windows;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Analytics;

/// <summary>
/// Copies an acknowledged report's numeric answers into report_metrics (FR-DSH-002 — indicators
/// count acknowledged reports only). Replaces whatever the report had before, so re-running it is
/// harmless. Only Number, Currency and Percentage fields count as figures; the caller saves.
/// </summary>
public static class ReportMetricWriter
{
    public static readonly HashSet<TemplateFieldType> FigureTypes =
        [TemplateFieldType.Number, TemplateFieldType.Currency, TemplateFieldType.Percentage];

    public static async Task ReplaceAsync(
        IAppDbContext db, ReportInstance instance, SubmissionWindow window, CancellationToken cancellationToken)
    {
        var existing = await db.ReportMetrics.Where(m => m.ReportInstanceId == instance.Id).ToListAsync(cancellationToken);
        db.ReportMetrics.RemoveRange(existing);

        var unitPath = await db.Units.AsNoTracking().Where(u => u.Id == instance.UnitId).Select(u => u.Path).FirstAsync(cancellationToken);
        var schemaJson = await db.TemplateVersions.AsNoTracking()
            .Where(v => v.Id == instance.TemplateVersionId).Select(v => v.SchemaJson).FirstAsync(cancellationToken);
        var schema = JsonSerializer.Deserialize<TemplateSchema>(schemaJson, JsonSerializerOptions.Web)!;
        var answers = JsonSerializer.Deserialize<Dictionary<string, object?>>(instance.AnswersJson, JsonSerializerOptions.Web) ?? [];

        foreach (var field in schema.Sections.SelectMany(s => s.Fields).Where(f => FigureTypes.Contains(f.Type)).DistinctBy(f => f.Key))
        {
            if (AnswerValue.ToDoubleOrNull(answers.GetValueOrDefault(field.Key)) is not { } number || !double.IsFinite(number))
            {
                continue;
            }

            db.ReportMetrics.Add(ReportMetric.Create(
                Guid.CreateVersion7(), instance.Id, instance.WindowId, instance.UnitId, unitPath, field.Key, (decimal)number, window.OpensAt));
        }
    }
}
