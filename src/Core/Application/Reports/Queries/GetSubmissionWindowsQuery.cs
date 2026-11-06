using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Application.Reports.Common;
using ManagementApi.Application.Reports.DTOs;
using ManagementApi.Domain.Entities.Reports;
using ManagementApi.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ManagementApi.Application.Reports.Queries;

public record GetSubmissionWindowsQuery(Guid? ReportTemplateId = null) : IRequest<List<SubmissionWindowDto>>;

public class GetSubmissionWindowsQueryHandler : IRequestHandler<GetSubmissionWindowsQuery, List<SubmissionWindowDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _clock;

    public GetSubmissionWindowsQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser, TimeProvider clock)
    {
        _context = context;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<List<SubmissionWindowDto>> Handle(GetSubmissionWindowsQuery request, CancellationToken cancellationToken)
    {
        var now = _clock.GetUtcNow().UtcDateTime;

        var query = _context.SubmissionWindows
            .Include(w => w.ReportTemplate)
            .Include(w => w.Extensions)
            .AsNoTracking()
            .AsQueryable();

        if (request.ReportTemplateId.HasValue)
        {
            query = query.Where(w => w.ReportTemplateId == request.ReportTemplateId.Value);
        }

        var windows = await query.OrderByDescending(w => w.StartDate).ToListAsync(cancellationToken);
        var windowIds = windows.Select(w => w.Id).ToList();

        // Submitted reports per window (drafts and missed reports are not submissions)
        var counts = await _context.ReportSubmissions
            .Where(s => s.SubmissionWindowId != null && windowIds.Contains(s.SubmissionWindowId.Value))
            .Where(s => s.Status != SubmissionStatus.Draft && s.Status != SubmissionStatus.Missed)
            .GroupBy(s => s.SubmissionWindowId!.Value)
            .Select(g => new { WindowId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.WindowId, x => x.Count, cancellationToken);

        var ownUnit = OwnUnit();
        var isSecretariat = ReportingScope.IsSecretariat(_currentUser);
        var unitNames = isSecretariat ? await ExtensionUnitNamesAsync(windows, cancellationToken) : new Dictionary<Guid, string>();

        return windows.Select(w => new SubmissionWindowDto
        {
            Id = w.Id,
            ReportTemplateId = w.ReportTemplateId,
            ReportTemplateName = w.ReportTemplate?.Name ?? "",
            Name = w.Name,
            Description = w.Description,
            StartDate = w.StartDate,
            EndDate = w.EndDate,
            MuqamDueAt = w.MuqamDueAt,
            DilaDueAt = w.DilaDueAt,
            ZoneDueAt = w.ZoneDueAt,
            IsActive = w.IsActive,
            IsOpen = w.IsOpen(now),
            Phase = !w.IsActive ? "Inactive" : now < w.StartDate ? "Upcoming" : now <= w.EndDate ? "Open" : "Closed",
            SubmissionCount = counts.GetValueOrDefault(w.Id),
            CreatedOn = w.CreatedOn,
            Warnings = w.DueDateWarnings().ToList(),
            // Only the Secretariat sees every unit's extension; a head sees their own through MyDueAt.
            Extensions = isSecretariat
                ? w.Extensions.Select(e => new WindowExtensionDto
                {
                    Id = e.Id,
                    OrganizationLevel = e.OrganizationLevel.ToString(),
                    UnitId = e.UnitId,
                    UnitName = unitNames.GetValueOrDefault(e.UnitId),
                    DueAt = e.DueAt,
                    ClosesAt = e.ClosesAt,
                    Reason = e.Reason
                }).ToList()
                : new List<WindowExtensionDto>(),
            MyDueAt = ownUnit is { } u ? w.DueAtFor(u.Level, u.UnitId) : null,
            MyClosesAt = ownUnit is { } c ? w.ClosesAtFor(c.Level, c.UnitId) : null,
            IsOpenForMe = ownUnit is { } o ? w.IsOpenFor(now, o.Level, o.UnitId) : null,
        }).ToList();
    }

    private (OrganizationLevel Level, Guid UnitId)? OwnUnit() => _currentUser.OrganizationLevel switch
    {
        OrganizationLevel.Muqam when _currentUser.MuqamId.HasValue => (OrganizationLevel.Muqam, _currentUser.MuqamId.Value),
        OrganizationLevel.Dila when _currentUser.DilaId.HasValue => (OrganizationLevel.Dila, _currentUser.DilaId.Value),
        OrganizationLevel.Zone when _currentUser.ZoneId.HasValue => (OrganizationLevel.Zone, _currentUser.ZoneId.Value),
        _ => null
    };

    private async Task<Dictionary<Guid, string>> ExtensionUnitNamesAsync(List<SubmissionWindow> windows, CancellationToken cancellationToken)
    {
        var ids = windows.SelectMany(w => w.Extensions).Select(e => e.UnitId).Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        var names = new Dictionary<Guid, string>();
        foreach (var m in await _context.Muqams.Where(x => ids.Contains(x.Id)).Select(x => new { x.Id, x.Name }).ToListAsync(cancellationToken)) names[m.Id] = m.Name;
        foreach (var d in await _context.Dilas.Where(x => ids.Contains(x.Id)).Select(x => new { x.Id, x.Name }).ToListAsync(cancellationToken)) names[d.Id] = d.Name;
        foreach (var z in await _context.Zones.Where(x => ids.Contains(x.Id)).Select(x => new { x.Id, x.Name }).ToListAsync(cancellationToken)) names[z.Id] = z.Name;
        return names;
    }
}
