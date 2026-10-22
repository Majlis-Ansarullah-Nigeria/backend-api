using System.Text.Json;
using HRS.Application.Common.Interfaces;
using HRS.Application.Features.Templates.TemplateVersions;
using HRS.Domain.Templates;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Analytics;

public static class IndicatorFields
{
    /// <summary>The figures of the newest published template version (or the newest draft, before
    /// anything is published), in template order.</summary>
    public static async Task<IReadOnlyList<IndicatorField>> LoadAsync(IAppDbContext db, Guid reportTypeId, CancellationToken cancellationToken)
    {
        var versions = await db.TemplateVersions.AsNoTracking()
            .Where(v => v.ReportTypeId == reportTypeId)
            .Select(v => new { v.VersionNumber, v.Status, v.SchemaJson })
            .ToListAsync(cancellationToken);
        var version = versions
            .OrderBy(v => v.Status == TemplateVersionStatus.Published ? 0 : 1)
            .ThenByDescending(v => v.VersionNumber)
            .FirstOrDefault();
        if (version is null)
        {
            return [];
        }

        var schema = JsonSerializer.Deserialize<TemplateSchema>(version.SchemaJson, JsonSerializerOptions.Web)!;
        return [.. schema.Sections
            .SelectMany(s => s.Fields)
            .Where(f => ReportMetricWriter.FigureTypes.Contains(f.Type))
            .DistinctBy(f => f.Key)
            .Select(f => new IndicatorField(f.Key, f.Label, f.Type, f.UnitLabel, f.Aggregation))];
    }
}
