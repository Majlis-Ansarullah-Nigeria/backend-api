using System.Text.Json;
using HRS.Application.Common.Interfaces;
using HRS.Application.Features.Templates.TemplateVersions;
using HRS.Domain.Templates;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Analytics;

/// <summary>A template section and the figures in it.</summary>
public sealed record IndicatorSection(string Key, string Title, IReadOnlyList<IndicatorField> Fields);

public static class IndicatorFields
{
    /// <summary>The figures of the newest published template version (or the newest draft, before
    /// anything is published), in template order.</summary>
    public static async Task<IReadOnlyList<IndicatorField>> LoadAsync(IAppDbContext db, Guid reportTypeId, CancellationToken cancellationToken) =>
        [.. (await LoadSectionsAsync(db, reportTypeId, cancellationToken)).SelectMany(s => s.Fields)];

    /// <summary>The same figures, grouped by the section they sit in. Sections without figures are
    /// left out; a field key appears only once, in the first section that has it.</summary>
    public static async Task<IReadOnlyList<IndicatorSection>> LoadSectionsAsync(IAppDbContext db, Guid reportTypeId, CancellationToken cancellationToken)
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
        var seen = new HashSet<string>();
        var sections = new List<IndicatorSection>();
        foreach (var section in schema.Sections)
        {
            var fields = section.Fields
                .Where(f => ReportMetricWriter.FigureTypes.Contains(f.Type) && seen.Add(f.Key))
                .Select(f => new IndicatorField(f.Key, f.Label, f.Type, f.UnitLabel, f.Aggregation))
                .ToList();
            if (fields.Count > 0)
            {
                sections.Add(new IndicatorSection(section.Key, section.Title, fields));
            }
        }

        return sections;
    }
}
