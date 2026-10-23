using System.Globalization;
using System.Text;
using HRS.Application.Common.Interfaces;
using HRS.Domain.Access;
using HRS.Domain.Audit;
using HRS.Domain.Common;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Audit;

/// <summary>One row of the viewer. IpAddress is masked (IpMask); Before/After are the stored JSON.</summary>
public sealed record AuditEventRow(
    Guid Id,
    DateTimeOffset OccurredAt,
    Guid? ActorUserId,
    string ActorName,
    string Action,
    string EntityType,
    string? EntityId,
    Guid? UnitId,
    string? UnitName,
    string Summary,
    string? BeforeJson,
    string? AfterJson,
    string? IpAddress,
    string? UserAgent);

public sealed record AuditEventPage(IReadOnlyList<AuditEventRow> Rows, int TotalCount, int Page, int PageSize);

public sealed record AuditPersonOption(Guid UserId, string Name);

public sealed record AuditUnitOption(Guid UnitId, string Name, int Depth);

public sealed record AuditFilterOptions(IReadOnlyList<string> Actions, IReadOnlyList<AuditPersonOption> People, IReadOnlyList<AuditUnitOption> Units);

/// <summary>The viewer's filters (FR-AUD-002). Unit includes everything under it.</summary>
public sealed record AuditFilter(string? Action, Guid? ActorUserId, Guid? UnitId, DateTimeOffset? From, DateTimeOffset? To);

internal static class AuditAccess
{
    /// <summary>SRS §3.2 "View audit log": the Secretariat and the system administrator.</summary>
    public static async Task<bool> CanViewAsync(IAccessPolicy accessPolicy, Guid userId, CancellationToken cancellationToken) =>
        await accessPolicy.HasRoleAsync(userId, RoleName.Secretariat, cancellationToken)
        || await accessPolicy.HasRoleAsync(userId, RoleName.SysAdmin, cancellationToken);

    public static readonly Error Forbidden = Error.Forbidden("FORBIDDEN", "Only the Secretariat and the system administrator see the audit log.");

    public static async Task<IQueryable<AuditEvent>> ApplyAsync(IAppDbContext db, AuditFilter filter, CancellationToken cancellationToken)
    {
        var query = db.AuditEvents.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(filter.Action))
        {
            query = query.Where(e => e.Action == filter.Action);
        }

        if (filter.ActorUserId is { } actor)
        {
            query = query.Where(e => e.ActorUserId == actor);
        }

        if (filter.UnitId is { } unitId)
        {
            var path = await db.Units.AsNoTracking().Where(u => u.Id == unitId).Select(u => u.Path).FirstOrDefaultAsync(cancellationToken);
            var unitIds = path is null
                ? [unitId]
                : await db.Units.AsNoTracking().Where(u => u.Path == path || u.Path.StartsWith(path + ".")).Select(u => u.Id).ToListAsync(cancellationToken);
            query = query.Where(e => e.UnitId != null && unitIds.Contains(e.UnitId.Value));
        }

        if (filter.From is { } from)
        {
            query = query.Where(e => e.OccurredAt >= from);
        }

        if (filter.To is { } to)
        {
            query = query.Where(e => e.OccurredAt < to);
        }

        return query.OrderByDescending(e => e.OccurredAt).ThenByDescending(e => e.Id);
    }

    public static async Task<List<AuditEventRow>> ToRowsAsync(
        IAppDbContext db, IIdentityService identityService, List<AuditEvent> events, bool maskIp, CancellationToken cancellationToken)
    {
        var actorIds = events.Where(e => e.ActorUserId is not null).Select(e => e.ActorUserId!.Value).Distinct().ToList();
        IReadOnlyDictionary<Guid, string> names = actorIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await identityService.GetUserNamesAsync(actorIds, cancellationToken);
        var unitIds = events.Where(e => e.UnitId is not null).Select(e => e.UnitId!.Value).Distinct().ToList();
        var unitNames = await db.Units.AsNoTracking().Where(u => unitIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Name, cancellationToken);

        return [.. events.Select(e => new AuditEventRow(
            e.Id,
            e.OccurredAt,
            e.ActorUserId,
            e.ActorUserId is { } id ? names.GetValueOrDefault(id, "Unknown user") : e.ActorEmail ?? "System",
            e.Action,
            e.EntityType,
            e.EntityId,
            e.UnitId,
            e.UnitId is { } unitId ? unitNames.GetValueOrDefault(unitId) : null,
            e.Summary,
            e.BeforeJson,
            e.AfterJson,
            maskIp ? IpMask.Mask(e.IpAddress) : e.IpAddress,
            e.UserAgent))];
    }
}

/// <summary>FR-AUD-002's audit log viewer: newest first, filtered, paginated.</summary>
public sealed record GetAuditEventsQuery(Guid UserId, AuditFilter Filter, int Page, int PageSize) : IRequest<Result<AuditEventPage>>;

public sealed class GetAuditEventsQueryHandler(IAppDbContext db, IAccessPolicy accessPolicy, IIdentityService identityService)
    : IRequestHandler<GetAuditEventsQuery, Result<AuditEventPage>>
{
    public async ValueTask<Result<AuditEventPage>> Handle(GetAuditEventsQuery request, CancellationToken cancellationToken)
    {
        if (!await AuditAccess.CanViewAsync(accessPolicy, request.UserId, cancellationToken))
        {
            return Result.Failure<AuditEventPage>(AuditAccess.Forbidden);
        }

        var page = Math.Max(request.Page, 1);
        var pageSize = Math.Clamp(request.PageSize <= 0 ? 50 : request.PageSize, 1, 200);
        var query = await AuditAccess.ApplyAsync(db, request.Filter, cancellationToken);
        var total = await query.CountAsync(cancellationToken);
        var events = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        var rows = await AuditAccess.ToRowsAsync(db, identityService, events, maskIp: true, cancellationToken);

        return Result.Success(new AuditEventPage(rows, total, page, pageSize));
    }
}

/// <summary>The viewer's dropdowns: every audited action, everyone who appears in the log, and
/// every unit (indented by depth).</summary>
public sealed record GetAuditFilterOptionsQuery(Guid UserId) : IRequest<Result<AuditFilterOptions>>;

public sealed class GetAuditFilterOptionsQueryHandler(IAppDbContext db, IAccessPolicy accessPolicy, IIdentityService identityService)
    : IRequestHandler<GetAuditFilterOptionsQuery, Result<AuditFilterOptions>>
{
    public async ValueTask<Result<AuditFilterOptions>> Handle(GetAuditFilterOptionsQuery request, CancellationToken cancellationToken)
    {
        if (!await AuditAccess.CanViewAsync(accessPolicy, request.UserId, cancellationToken))
        {
            return Result.Failure<AuditFilterOptions>(AuditAccess.Forbidden);
        }

        var actorIds = await db.AuditEvents.AsNoTracking()
            .Where(e => e.ActorUserId != null).Select(e => e.ActorUserId!.Value).Distinct().ToListAsync(cancellationToken);
        var names = actorIds.Count == 0 ? new Dictionary<Guid, string>() : await identityService.GetUserNamesAsync(actorIds, cancellationToken);
        var people = names.Select(n => new AuditPersonOption(n.Key, n.Value)).OrderBy(p => p.Name, StringComparer.Ordinal).ToList();

        var units = (await db.Units.AsNoTracking().Select(u => new { u.Id, u.Name, u.Path }).ToListAsync(cancellationToken))
            .OrderBy(u => u.Path, StringComparer.Ordinal)
            .Select(u => new AuditUnitOption(u.Id, u.Name, u.Path.Count(c => c == '.')))
            .ToList();

        return Result.Success(new AuditFilterOptions(AuditActions.All, people, units));
    }
}

public sealed record AuditCsvFile(byte[] Content, string FileName, int RowCount);

/// <summary>FR-AUD-002's CSV export: the same filters, full IP addresses (for investigations), up
/// to MaxRows rows, newest first.</summary>
public sealed record ExportAuditEventsCsvQuery(Guid UserId, AuditFilter Filter) : IRequest<Result<AuditCsvFile>>;

public sealed class ExportAuditEventsCsvQueryHandler(IAppDbContext db, IAccessPolicy accessPolicy, IIdentityService identityService, IClock clock)
    : IRequestHandler<ExportAuditEventsCsvQuery, Result<AuditCsvFile>>
{
    public const int MaxRows = 50_000;

    public async ValueTask<Result<AuditCsvFile>> Handle(ExportAuditEventsCsvQuery request, CancellationToken cancellationToken)
    {
        if (!await AuditAccess.CanViewAsync(accessPolicy, request.UserId, cancellationToken))
        {
            return Result.Failure<AuditCsvFile>(AuditAccess.Forbidden);
        }

        var query = await AuditAccess.ApplyAsync(db, request.Filter, cancellationToken);
        var events = await query.Take(MaxRows).ToListAsync(cancellationToken);
        var rows = await AuditAccess.ToRowsAsync(db, identityService, events, maskIp: false, cancellationToken);

        var csv = new StringBuilder();
        csv.AppendLine("when_utc,who,who_user_id,action,entity_type,entity_id,unit,summary,before,after,ip,user_agent");
        foreach (var row in rows)
        {
            csv.AppendLine(string.Join(',', new[]
            {
                row.OccurredAt.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
                row.ActorName,
                row.ActorUserId?.ToString(),
                row.Action,
                row.EntityType,
                row.EntityId,
                row.UnitName,
                row.Summary,
                row.BeforeJson,
                row.AfterJson,
                row.IpAddress,
                row.UserAgent,
            }.Select(Csv.Escape)));
        }

        var fileName = $"audit-log-{clock.UtcNow.ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture)}.csv";
        return Result.Success(new AuditCsvFile(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray(), fileName, rows.Count));
    }
}

public static class Csv
{
    /// <summary>RFC 4180 quoting, plus a leading apostrophe on values a spreadsheet would run as a
    /// formula (CSV injection: =, +, -, @, tab, carriage return).</summary>
    public static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        if (value[0] is '=' or '+' or '-' or '@' or '\t' or '\r')
        {
            value = "'" + value;
        }

        return value.IndexOfAny([',', '"', '\n', '\r']) >= 0 ? $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"" : value;
    }
}
