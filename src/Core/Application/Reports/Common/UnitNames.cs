using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Application.Reports.DTOs;
using ManagementApi.Domain.Entities.Reports;
using Microsoft.EntityFrameworkCore;

namespace ManagementApi.Application.Reports.Common;

/// <summary>Looks up Muqam, Dila and Zone names for a page of reports in three queries.</summary>
public sealed class UnitNames
{
    private readonly Dictionary<Guid, string> _muqams;
    private readonly Dictionary<Guid, string> _dilas;
    private readonly Dictionary<Guid, string> _zones;

    private UnitNames(Dictionary<Guid, string> muqams, Dictionary<Guid, string> dilas, Dictionary<Guid, string> zones)
    {
        _muqams = muqams;
        _dilas = dilas;
        _zones = zones;
    }

    public static async Task<UnitNames> LoadAsync(
        IApplicationDbContext context,
        IReadOnlyCollection<ReportSubmission> submissions,
        CancellationToken cancellationToken)
    {
        var muqamIds = submissions.Where(s => s.MuqamId.HasValue).Select(s => s.MuqamId!.Value).Distinct().ToList();
        var dilaIds = submissions.Where(s => s.DilaId.HasValue).Select(s => s.DilaId!.Value).Distinct().ToList();
        var zoneIds = submissions.Where(s => s.ZoneId.HasValue).Select(s => s.ZoneId!.Value).Distinct().ToList();

        var muqams = await context.Muqams.Where(m => muqamIds.Contains(m.Id)).ToDictionaryAsync(m => m.Id, m => m.Name, cancellationToken);
        var dilas = await context.Dilas.Where(d => dilaIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id, d => d.Name, cancellationToken);
        var zones = await context.Zones.Where(z => zoneIds.Contains(z.Id)).ToDictionaryAsync(z => z.Id, z => z.Name, cancellationToken);

        return new UnitNames(muqams, dilas, zones);
    }

    public ReportSubmissionDto Apply(ReportSubmissionDto dto, ReportSubmission submission) => dto with
    {
        MuqamName = submission.MuqamId is { } m ? _muqams.GetValueOrDefault(m) : null,
        DilaName = submission.DilaId is { } d ? _dilas.GetValueOrDefault(d) : null,
        ZoneName = submission.ZoneId is { } z ? _zones.GetValueOrDefault(z) : null
    };
}
