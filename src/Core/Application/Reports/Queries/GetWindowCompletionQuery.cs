using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Application.Common.Models;
using ManagementApi.Application.Reports.Common;
using ManagementApi.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ManagementApi.Application.Reports.Queries;

/// <summary>
/// Who has reported in a window: every unit expected to report, its status, and totals per Zone and Dila.
/// Scope: the Secretariat sees every unit; a Zone head the units in their Zone; a Dila head the units
/// in their Dila; a Muqam head their own Muqam.
/// </summary>
public record GetWindowCompletionQuery(Guid WindowId) : IRequest<Result<WindowCompletionDto>>;

public record WindowCompletionDto
{
    public Guid WindowId { get; init; }
    public string WindowName { get; init; } = default!;
    public string TemplateName { get; init; } = default!;
    public string Level { get; init; } = default!; // lowest submitting level (older screens)
    public List<string> Levels { get; init; } = new();
    public DateTime OpensAt { get; init; }
    public DateTime ClosesAt { get; init; }
    public CompletionCounts Totals { get; init; } = new();
    public List<CompletionGroupDto> ByZone { get; init; } = new();
    public List<CompletionGroupDto> ByDila { get; init; } = new();
    public List<CompletionUnitDto> Units { get; init; } = new();
}

public record CompletionCounts
{
    public int Expected { get; init; }
    public int Submitted { get; init; }    // submitted, in review, acknowledged or returned
    public int Acknowledged { get; init; }
    public int Returned { get; init; }
    public int Late { get; init; }
    public int Missed { get; init; }
    public int Draft { get; init; }
    public int NotStarted { get; init; }
    public int Overdue { get; init; }      // not started or draft after the unit's due date
    public decimal CompletionRate { get; init; }
}

public record CompletionGroupDto
{
    public Guid? Id { get; init; }
    public string Name { get; init; } = default!;
    public CompletionCounts Counts { get; init; } = new();
}

public record CompletionUnitDto
{
    public Guid UnitId { get; init; }
    public string Level { get; init; } = default!;
    public string Name { get; init; } = default!;
    public string? ZoneName { get; init; }
    public string? DilaName { get; init; }
    public string Status { get; init; } = default!; // NotStarted, Draft, Submitted, InReview, Acknowledged, Returned, Missed
    public bool IsLate { get; init; }
    public bool IsOverdue { get; init; }
    public DateTime DueAt { get; init; }
    public DateTime? SubmittedAt { get; init; }
    public string? ReferenceNumber { get; init; }
    public Guid? ReportId { get; init; }
}

public class GetWindowCompletionQueryHandler : IRequestHandler<GetWindowCompletionQuery, Result<WindowCompletionDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _clock;

    public GetWindowCompletionQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser, TimeProvider clock)
    {
        _context = context;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<Result<WindowCompletionDto>> Handle(GetWindowCompletionQuery request, CancellationToken cancellationToken)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        var window = await _context.SubmissionWindows
            .Include(w => w.ReportTemplate)
            .Include(w => w.Extensions)
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == request.WindowId, cancellationToken);

        if (window is null)
        {
            return Result<WindowCompletionDto>.Failure("Submission window not found");
        }

        var levels = window.ReportTemplate.SubmittingLevels;
        var units = InScope(await ExpectedUnits.ForLevelsAsync(_context, levels, cancellationToken)).ToList();

        var reports = (await _context.ReportSubmissions
            .AsNoTracking()
            .Where(s => s.SubmissionWindowId == window.Id)
            .Select(s => new { s.Id, s.OrganizationLevel, s.UnitId, s.Status, s.IsLate, s.SubmittedAt, s.ReferenceNumber })
            .ToListAsync(cancellationToken))
            .ToDictionary(s => (s.OrganizationLevel, s.UnitId));

        var zoneNames = await _context.Zones.AsNoTracking().ToDictionaryAsync(z => z.Id, z => z.Name, cancellationToken);
        var dilaNames = await _context.Dilas.AsNoTracking().ToDictionaryAsync(d => d.Id, d => d.Name, cancellationToken);
        var canOpenReports = ReportingScope.IsSecretariat(_currentUser);

        var rows = units.Select(u =>
        {
            reports.TryGetValue((u.Level, u.UnitId), out var report);
            var status = report?.Status.ToString() ?? "NotStarted";
            var dueAt = window.DueAtFor(u.Level, u.UnitId);
            var unstarted = report is null || report.Status == SubmissionStatus.Draft;
            return new CompletionUnitDto
            {
                UnitId = u.UnitId,
                Level = u.Level.ToString(),
                Name = u.Name,
                ZoneName = u.ZoneId is { } z ? zoneNames.GetValueOrDefault(z) : null,
                DilaName = u.DilaId is { } d ? dilaNames.GetValueOrDefault(d) : null,
                Status = status,
                IsLate = report?.IsLate ?? false,
                IsOverdue = unstarted && now > dueAt,
                DueAt = dueAt,
                SubmittedAt = report?.SubmittedAt,
                ReferenceNumber = report?.ReferenceNumber,
                ReportId = canOpenReports || IsOwn(u) ? report?.Id : null
            };
        }).OrderBy(r => r.ZoneName).ThenBy(r => r.DilaName).ThenBy(r => r.Name).ToList();

        return Result<WindowCompletionDto>.Success(new WindowCompletionDto
        {
            WindowId = window.Id,
            WindowName = window.Name,
            TemplateName = window.ReportTemplate.Name,
            Level = levels.Min().ToString(),
            Levels = levels.Select(l => l.ToString()).ToList(),
            OpensAt = window.StartDate,
            ClosesAt = window.EndDate,
            Totals = Count(rows),
            ByZone = rows.GroupBy(r => r.ZoneName ?? "No zone")
                .Select(g => new CompletionGroupDto { Name = g.Key, Counts = Count(g) })
                .OrderBy(g => g.Name).ToList(),
            ByDila = levels.Contains(OrganizationLevel.Muqam) || levels.Contains(OrganizationLevel.Dila)
                ? rows.Where(r => r.DilaName != null).GroupBy(r => r.DilaName!)
                    .Select(g => new CompletionGroupDto { Name = g.Key, Counts = Count(g) })
                    .OrderBy(g => g.Name).ToList()
                : new List<CompletionGroupDto>(),
            Units = rows
        });
    }

    private IEnumerable<ExpectedUnit> InScope(IEnumerable<ExpectedUnit> units)
    {
        if (ReportingScope.IsSecretariat(_currentUser) || _currentUser.OrganizationLevel == OrganizationLevel.National)
        {
            return units;
        }

        return _currentUser.OrganizationLevel switch
        {
            OrganizationLevel.Zone when _currentUser.ZoneId is { } z => units.Where(u => u.ZoneId == z),
            OrganizationLevel.Dila when _currentUser.DilaId is { } d => units.Where(u => u.DilaId == d),
            OrganizationLevel.Muqam when _currentUser.MuqamId is { } m => units.Where(u => u.MuqamId == m),
            _ => Enumerable.Empty<ExpectedUnit>()
        };
    }

    private bool IsOwn(ExpectedUnit unit) =>
        unit.Level == _currentUser.OrganizationLevel && unit.Level switch
        {
            OrganizationLevel.Muqam => _currentUser.MuqamId == unit.UnitId,
            OrganizationLevel.Dila => _currentUser.DilaId == unit.UnitId,
            OrganizationLevel.Zone => _currentUser.ZoneId == unit.UnitId,
            _ => false
        };

    private static CompletionCounts Count(IEnumerable<CompletionUnitDto> rows)
    {
        var list = rows.ToList();
        var submitted = list.Count(r => r.Status is "Submitted" or "InReview" or "Acknowledged" or "Returned");
        return new CompletionCounts
        {
            Expected = list.Count,
            Submitted = submitted,
            Acknowledged = list.Count(r => r.Status == "Acknowledged"),
            Returned = list.Count(r => r.Status == "Returned"),
            Late = list.Count(r => r.IsLate),
            Missed = list.Count(r => r.Status == "Missed"),
            Draft = list.Count(r => r.Status == "Draft"),
            NotStarted = list.Count(r => r.Status == "NotStarted"),
            Overdue = list.Count(r => r.IsOverdue),
            CompletionRate = list.Count == 0 ? 0 : Math.Round(100m * submitted / list.Count, 1)
        };
    }
}
