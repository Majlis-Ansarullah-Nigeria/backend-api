using System.Text.Json;
using HRS.Application.Common.Interfaces;
using HRS.Application.Features.Organization.Units;
using HRS.Application.Features.Submissions.Review;
using HRS.Application.Features.Templates.TemplateVersions;
using HRS.Domain.Access;
using HRS.Domain.Common;
using HRS.Domain.Organization;
using HRS.Domain.Submissions;
using HRS.Domain.Templates;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Review.Department;

/// <summary>FR-REV-005: "each department officer MUST have read-only access to their
/// department's sections in every unit's report, at every level." Scoped to reports that have
/// reached Submitted at least once (same IncomingStatuses as the Secretariat inbox — nothing to
/// see before that); unlike the inbox, NOT restricted to a particular ReportType.RecipientLevel,
/// since department ownership has nothing to do with who receives the report. Values are never
/// masked here (FR-AC-006's confidentiality rule is for observers; a department officer is a
/// distinct, explicitly-granted read scope, same reasoning the Secretariat inbox uses).</summary>
public sealed record GetDepartmentReportsQuery(
    Guid UserId,
    Guid? ReportTypeId,
    Guid? WindowId,
    UnitLevelCode? UnitLevel,
    Guid? ZoneUnitId) : IRequest<Result<IReadOnlyList<DepartmentReportRow>>>;

public sealed class GetDepartmentReportsQueryHandler(IAppDbContext db, IClock clock)
    : IRequestHandler<GetDepartmentReportsQuery, Result<IReadOnlyList<DepartmentReportRow>>>
{
    private static readonly HashSet<ReportInstanceStatus> IncomingStatuses =
        [ReportInstanceStatus.Submitted, ReportInstanceStatus.InReview, ReportInstanceStatus.Returned, ReportInstanceStatus.Acknowledged];

    public async ValueTask<Result<IReadOnlyList<DepartmentReportRow>>> Handle(
        GetDepartmentReportsQuery request, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var departmentUnitIds = await db.RoleAssignments.AsNoTracking()
            .Where(a => a.UserId == request.UserId && a.Role == RoleName.DeptOfficer
                && a.StartsAt <= now && (a.EndsAt == null || a.EndsAt > now))
            .Select(a => a.UnitId)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (departmentUnitIds.Count == 0)
        {
            return Result.Failure<IReadOnlyList<DepartmentReportRow>>(
                Error.Forbidden("FORBIDDEN", "Only department officers have department data access."));
        }

        var departmentUnitIdSet = departmentUnitIds.ToHashSet();

        var instances = await db.ReportInstances.AsNoTracking()
            .Where(i => IncomingStatuses.Contains(i.Status))
            .ToListAsync(cancellationToken);

        var windowIds = instances.Select(i => i.WindowId).Distinct().ToList();
        var windows = await db.SubmissionWindows.AsNoTracking().Where(w => windowIds.Contains(w.Id)).ToListAsync(cancellationToken);
        if (request.WindowId is { } windowId)
        {
            windows = [.. windows.Where(w => w.Id == windowId)];
        }

        var reportTypeIds = windows.Select(w => w.ReportTypeId).Distinct().ToList();
        var reportTypes = await db.ReportTypes.AsNoTracking().Where(r => reportTypeIds.Contains(r.Id)).ToListAsync(cancellationToken);
        if (request.ReportTypeId is { } reportTypeId)
        {
            reportTypes = [.. reportTypes.Where(r => r.Id == reportTypeId)];
        }

        var reportTypeById = reportTypes.ToDictionary(r => r.Id);
        var windowsInScope = windows.Where(w => reportTypeById.ContainsKey(w.ReportTypeId)).ToDictionary(w => w.Id);

        var unitIds = instances.Select(i => i.UnitId).Distinct().ToList();
        var units = await db.Units.AsNoTracking()
            .Where(u => unitIds.Contains(u.Id))
            .Join(db.UnitLevels.AsNoTracking(), u => u.LevelId, l => l.Id, (u, l) => new { Unit = u, LevelCode = l.Code })
            .ToListAsync(cancellationToken);
        var unitById = units.ToDictionary(u => u.Unit.Id);

        var allUnits = await db.Units.AsNoTracking().ToListAsync(cancellationToken);
        var unitByPath = allUnits.ToDictionary(u => u.Path);

        var templateVersionIds = instances.Select(i => i.TemplateVersionId).Distinct().ToList();
        var templateVersions = await db.TemplateVersions.AsNoTracking()
            .Where(v => templateVersionIds.Contains(v.Id))
            .ToListAsync(cancellationToken);
        var schemaByTemplateVersionId = templateVersions.ToDictionary(
            v => v.Id, v => JsonSerializer.Deserialize<TemplateSchema>(v.SchemaJson, JsonSerializerOptions.Web)!);

        var rows = new List<DepartmentReportRow>();
        var ownedFieldKeysByInstance = new Dictionary<Guid, HashSet<string>>();

        foreach (var instance in instances)
        {
            if (!windowsInScope.TryGetValue(instance.WindowId, out var window)
                || !reportTypeById.TryGetValue(window.ReportTypeId, out var reportType)
                || !unitById.TryGetValue(instance.UnitId, out var unitEntry)
                || !schemaByTemplateVersionId.TryGetValue(instance.TemplateVersionId, out var schema))
            {
                continue;
            }

            if (request.UnitLevel is { } unitLevel && unitEntry.LevelCode != unitLevel)
            {
                continue;
            }

            var (zoneUnitId, zoneName) = ZoneResolver.Resolve(unitEntry.Unit.Path, unitEntry.LevelCode, unitByPath);
            if (request.ZoneUnitId is { } requestedZoneUnitId && zoneUnitId != requestedZoneUnitId)
            {
                continue;
            }

            var answers = JsonSerializer.Deserialize<Dictionary<string, object?>>(instance.AnswersJson, JsonSerializerOptions.Web) ?? [];
            var ownedFields = schema.Sections
                .SelectMany(s => FieldVisibility.VisibleFields(s.Fields, unitEntry.LevelCode, answers))
                .Where(f => f.Type != TemplateFieldType.Computed && f.OwningDepartmentId is { } owner && departmentUnitIdSet.Contains(owner))
                .ToList();

            if (ownedFields.Count == 0)
            {
                continue;
            }

            ownedFieldKeysByInstance[instance.Id] = [.. ownedFields.Select(f => f.Key)];

            rows.Add(new DepartmentReportRow(
                instance.Id, reportType.Id, reportType.Name, unitEntry.Unit.Id, unitEntry.Unit.Name, unitEntry.LevelCode,
                zoneUnitId, zoneName, window.Id, window.PeriodLabel, instance.Status, instance.SubmittedAt,
                [])); // Fields filled in below, once attachments are loaded.
        }

        if (rows.Count == 0)
        {
            return Result.Success<IReadOnlyList<DepartmentReportRow>>([]);
        }

        var attachments = await db.ReportAttachments.AsNoTracking()
            .Where(a => ownedFieldKeysByInstance.Keys.Contains(a.ReportInstanceId) && a.Status == ReportAttachmentStatus.Clean)
            .ToListAsync(cancellationToken);
        var attachmentsByInstanceAndField = attachments
            .GroupBy(a => a.ReportInstanceId)
            .ToDictionary(
                g => g.Key,
                g => g.GroupBy(a => a.FieldKey).ToDictionary(fg => fg.Key, IReadOnlyList<string> (fg) => [.. fg.Select(a => a.FileName)]));

        var instanceById = instances.ToDictionary(i => i.Id);
        var finished = new List<DepartmentReportRow>();
        foreach (var row in rows)
        {
            var instance = instanceById[row.ReportId];
            var schema = schemaByTemplateVersionId[instance.TemplateVersionId];
            var answers = JsonSerializer.Deserialize<Dictionary<string, object?>>(instance.AnswersJson, JsonSerializerOptions.Web) ?? [];
            var ownedFieldKeys = ownedFieldKeysByInstance[row.ReportId];
            var attachmentsByField = attachmentsByInstanceAndField.GetValueOrDefault(
                row.ReportId, new Dictionary<string, IReadOnlyList<string>>());

            var fields = schema.Sections
                .SelectMany(s => s.Fields)
                .Where(f => ownedFieldKeys.Contains(f.Key))
                .Select(f => new DepartmentFieldValue(f.Key, f.Label, AnswerDisplay.Format(f, answers.GetValueOrDefault(f.Key), attachmentsByField)))
                .ToList();

            finished.Add(row with { Fields = fields });
        }

        IReadOnlyList<DepartmentReportRow> sorted = [.. finished.OrderByDescending(r => r.SubmittedAt)];
        return Result.Success(sorted);
    }
}
