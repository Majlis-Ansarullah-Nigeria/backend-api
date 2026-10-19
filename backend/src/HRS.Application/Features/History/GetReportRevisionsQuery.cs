using System.Text.Json;
using HRS.Application.Common.Interfaces;
using HRS.Application.Features.Templates.TemplateVersions;
using HRS.Domain.Common;
using HRS.Domain.Submissions;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.History;

/// <summary>One entry of a report's version history; Changes is the diff against the entry
/// before it (empty for the first).</summary>
public sealed record ReportRevisionEntry(
    int RevisionNo,
    ReportRevisionAction Action,
    DateTimeOffset CreatedAt,
    string? ActorName,
    IReadOnlyList<ReportFieldChange> Changes);

public sealed record ReportRevisionHistory(
    Guid ReportId,
    string ReportTypeName,
    string PeriodLabel,
    string UnitName,
    IReadOnlyList<ReportRevisionEntry> Revisions);

/// <summary>FR-HIS-006: every submission, withdrawal and resubmission of one report, newest
/// first, each with a diff against the one before it.</summary>
public sealed record GetReportRevisionsQuery(Guid UserId, Guid ReportId) : IRequest<Result<ReportRevisionHistory>>;

public sealed class GetReportRevisionsQueryHandler(IAppDbContext db, IAccessPolicy accessPolicy, IIdentityService identityService, IClock clock)
    : IRequestHandler<GetReportRevisionsQuery, Result<ReportRevisionHistory>>
{
    public async ValueTask<Result<ReportRevisionHistory>> Handle(GetReportRevisionsQuery request, CancellationToken cancellationToken)
    {
        var instance = await db.ReportInstances.AsNoTracking().FirstOrDefaultAsync(i => i.Id == request.ReportId, cancellationToken);
        if (instance is null)
        {
            return Result.Failure<ReportRevisionHistory>(Error.NotFound("REPORT_NOT_FOUND", "That report doesn't exist."));
        }

        var window = await db.SubmissionWindows.AsNoTracking().FirstAsync(w => w.Id == instance.WindowId, cancellationToken);
        var reportType = await db.ReportTypes.AsNoTracking().FirstAsync(r => r.Id == window.ReportTypeId, cancellationToken);
        var unitRow = await db.Units.AsNoTracking()
            .Where(u => u.Id == instance.UnitId)
            .Join(db.UnitLevels.AsNoTracking(), u => u.LevelId, l => l.Id, (u, l) => new { u.Name, LevelCode = l.Code })
            .FirstAsync(cancellationToken);

        var access = await ReportReadAccess.ResolveAsync(
            db, accessPolicy, request.UserId, instance.UnitId, unitRow.LevelCode, reportType, clock.UtcNow, cancellationToken);
        if (access.IsFailure)
        {
            return Result.Failure<ReportRevisionHistory>(access.Error);
        }

        var revisions = await db.ReportRevisions.AsNoTracking()
            .Where(r => r.ReportInstanceId == instance.Id)
            .OrderBy(r => r.RevisionNo)
            .ToListAsync(cancellationToken);

        var templateVersion = await db.TemplateVersions.AsNoTracking().FirstAsync(v => v.Id == instance.TemplateVersionId, cancellationToken);
        var schema = JsonSerializer.Deserialize<TemplateSchema>(templateVersion.SchemaJson, JsonSerializerOptions.Web)!;

        var actorIds = revisions.Where(r => r.ActorUserId is not null).Select(r => r.ActorUserId!.Value).Distinct().ToList();
        IReadOnlyDictionary<Guid, string> names = actorIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await identityService.GetUserNamesAsync(actorIds, cancellationToken);

        var entries = new List<ReportRevisionEntry>(revisions.Count);
        string? previousAnswers = null;
        foreach (var revision in revisions)
        {
            var changes = previousAnswers is null
                ? []
                : AnswerDiff.Compare(schema, unitRow.LevelCode, previousAnswers, revision.AnswersJson, access.Value.HideConfidential);
            var actorName = revision.ActorUserId is { } actorId ? names.GetValueOrDefault(actorId) : null;
            entries.Add(new ReportRevisionEntry(revision.RevisionNo, revision.Action, revision.CreatedAt, actorName, changes));
            previousAnswers = revision.AnswersJson;
        }

        entries.Reverse();

        return Result.Success(new ReportRevisionHistory(instance.Id, reportType.Name, window.PeriodLabel, unitRow.Name, entries));
    }
}
