using System.Text.Json;
using HRS.Application.Common.Interfaces;
using HRS.Application.Features.Submissions.Review;
using HRS.Application.Features.Templates.TemplateVersions;
using HRS.Domain.Admin;
using HRS.Domain.Common;
using HRS.Domain.Organization;
using HRS.Domain.Submissions;
using HRS.Domain.Templates;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.History;

/// <summary>A period the unit has a report for. IsDraft marks the unit's own unsubmitted draft,
/// offered only to the unit's head (the mockup's "September 2026 (draft)").</summary>
public sealed record ComparisonPeriod(Guid WindowId, string PeriodLabel, DateTimeOffset OpensAt, bool IsDraft);

/// <summary>One numeric field's value in period A and period B. Null means unanswered, or the
/// field isn't in that period's template version.</summary>
public sealed record ComparisonFigure(string FieldKey, string Label, TemplateFieldType Type, string? UnitLabel, double? PeriodA, double? PeriodB);

/// <summary>Every figure's value in one period, for the trend chart. Keyed by FieldKey.</summary>
public sealed record ComparisonSeriesPoint(Guid WindowId, string PeriodLabel, IReadOnlyDictionary<string, double?> Values);

public sealed record PeriodComparison(
    Guid UnitId,
    string UnitName,
    Guid ReportTypeId,
    string ReportTypeName,
    IReadOnlyList<ComparisonPeriod> Periods,
    Guid? PeriodAWindowId,
    Guid? PeriodBWindowId,
    IReadOnlyList<ComparisonFigure> Figures,
    IReadOnlyList<string> ChangedTextSections,
    IReadOnlyList<ComparisonSeriesPoint> Series);

/// <summary>
/// FR-HIS-005: the same unit's report in two periods side by side — every numeric field's value
/// in each, which text sections changed, and the trailing 12-period trend ending at period B.
/// Defaults to the two most recent periods. Fields are matched by their stable key, so
/// comparing across template versions works; labels come from period B's version first.
/// </summary>
public sealed record GetPeriodComparisonQuery(Guid UserId, Guid UnitId, Guid ReportTypeId, Guid? PeriodAWindowId, Guid? PeriodBWindowId)
    : IRequest<Result<PeriodComparison>>, IRequiresFeature
{
    public FeatureModule Feature => FeatureModule.ComparisonAndVersions;
}

public sealed class GetPeriodComparisonQueryHandler(IAppDbContext db, IAccessPolicy accessPolicy, IClock clock)
    : IRequestHandler<GetPeriodComparisonQuery, Result<PeriodComparison>>
{
    private const int SeriesLength = 12;

    private static readonly HashSet<ReportInstanceStatus> SubmittedStatuses =
    [
        ReportInstanceStatus.Submitted,
        ReportInstanceStatus.InReview,
        ReportInstanceStatus.Returned,
        ReportInstanceStatus.Acknowledged,
    ];

    private static readonly HashSet<TemplateFieldType> FigureTypes =
        [TemplateFieldType.Number, TemplateFieldType.Currency, TemplateFieldType.Percentage];

    private static readonly HashSet<TemplateFieldType> TextTypes =
        [TemplateFieldType.ShortText, TemplateFieldType.LongText];

    public async ValueTask<Result<PeriodComparison>> Handle(GetPeriodComparisonQuery request, CancellationToken cancellationToken)
    {
        var reportType = await db.ReportTypes.AsNoTracking().FirstOrDefaultAsync(r => r.Id == request.ReportTypeId, cancellationToken);
        var unitRow = await db.Units.AsNoTracking()
            .Where(u => u.Id == request.UnitId)
            .Join(db.UnitLevels.AsNoTracking(), u => u.LevelId, l => l.Id, (u, l) => new { u.Name, LevelCode = l.Code })
            .FirstOrDefaultAsync(cancellationToken);
        if (reportType is null || unitRow is null)
        {
            return Result.Failure<PeriodComparison>(Error.NotFound("NOT_FOUND", "That unit or report type doesn't exist."));
        }

        var access = await ReportReadAccess.ResolveAsync(
            db, accessPolicy, request.UserId, request.UnitId, unitRow.LevelCode, reportType, clock.UtcNow, cancellationToken);
        if (access.IsFailure)
        {
            return Result.Failure<PeriodComparison>(access.Error);
        }

        var grant = access.Value;
        var rows = await db.ReportInstances.AsNoTracking()
            .Where(i => i.UnitId == request.UnitId)
            .Join(db.SubmissionWindows.AsNoTracking().Where(w => w.ReportTypeId == request.ReportTypeId),
                i => i.WindowId, w => w.Id, (i, w) => new { Instance = i, Window = w })
            .ToListAsync(cancellationToken);

        var available = rows
            .Where(r => SubmittedStatuses.Contains(r.Instance.Status)
                || (grant.CanSeeDrafts && r.Instance.Status == ReportInstanceStatus.Draft && r.Instance.AnswersJson != "{}"))
            .OrderBy(r => r.Window.OpensAt)
            .ToList();

        var periods = available
            .Select(r => new ComparisonPeriod(r.Window.Id, r.Window.PeriodLabel, r.Window.OpensAt, r.Instance.Status == ReportInstanceStatus.Draft))
            .ToList();

        var empty = new PeriodComparison(
            request.UnitId, unitRow.Name, reportType.Id, reportType.Name, periods, null, null, [], [], []);
        if (available.Count == 0)
        {
            return Result.Success(empty);
        }

        var indexB = request.PeriodBWindowId is { } bId ? available.FindIndex(r => r.Window.Id == bId) : available.Count - 1;
        var indexA = request.PeriodAWindowId is { } aId ? available.FindIndex(r => r.Window.Id == aId) : Math.Max(indexB - 1, 0);
        if (indexA < 0 || indexB < 0)
        {
            return Result.Failure<PeriodComparison>(Error.NotFound("PERIOD_NOT_FOUND", "That period has no report to compare."));
        }

        var a = available[indexA];
        var b = available[indexB];

        var seriesRows = available.Take(indexB + 1).TakeLast(SeriesLength).ToList();
        var versionIds = seriesRows.Select(r => r.Instance.TemplateVersionId).Append(a.Instance.TemplateVersionId).Distinct().ToList();
        var schemaByVersion = (await db.TemplateVersions.AsNoTracking()
                .Where(v => versionIds.Contains(v.Id))
                .Select(v => new { v.Id, v.SchemaJson })
                .ToListAsync(cancellationToken))
            .ToDictionary(v => v.Id, v => JsonSerializer.Deserialize<TemplateSchema>(v.SchemaJson, JsonSerializerOptions.Web)!);

        bool Include(TemplateField f) =>
            FieldVisibility.IsVisibleForLevel(f, unitRow.LevelCode) && !(grant.HideConfidential && f.Confidential);

        var schemaA = schemaByVersion[a.Instance.TemplateVersionId];
        var schemaB = schemaByVersion[b.Instance.TemplateVersionId];

        // B's fields first, in B's order, then any A-only fields — matched by stable key.
        var figureFields = schemaB.Sections.Concat(schemaA.Sections)
            .SelectMany(s => s.Fields)
            .Where(f => FigureTypes.Contains(f.Type) && Include(f))
            .DistinctBy(f => f.Key)
            .ToList();

        var answersA = AnswerDiff.Parse(a.Instance.AnswersJson);
        var answersB = AnswerDiff.Parse(b.Instance.AnswersJson);

        var figures = figureFields
            .Select(f => new ComparisonFigure(
                f.Key, f.Label, f.Type, f.UnitLabel,
                AnswerValue.ToDoubleOrNull(answersA.GetValueOrDefault(f.Key)),
                AnswerValue.ToDoubleOrNull(answersB.GetValueOrDefault(f.Key))))
            .ToList();

        var changedTextSections = schemaB.Sections.Concat(schemaA.Sections)
            .SelectMany(s => s.Fields.Select(f => (Section: s.Title, Field: f)))
            .Where(x => TextTypes.Contains(x.Field.Type) && Include(x.Field))
            .DistinctBy(x => x.Field.Key)
            .Where(x => AnswerValue.ToComparableString(answersA.GetValueOrDefault(x.Field.Key))
                != AnswerValue.ToComparableString(answersB.GetValueOrDefault(x.Field.Key)))
            .Select(x => x.Section)
            .Distinct()
            .ToList();

        var series = seriesRows
            .Select(r =>
            {
                var answers = AnswerDiff.Parse(r.Instance.AnswersJson);
                IReadOnlyDictionary<string, double?> values = figureFields.ToDictionary(
                    f => f.Key, f => AnswerValue.ToDoubleOrNull(answers.GetValueOrDefault(f.Key)));
                return new ComparisonSeriesPoint(r.Window.Id, r.Window.PeriodLabel, values);
            })
            .ToList();

        return Result.Success(empty with
        {
            PeriodAWindowId = a.Window.Id,
            PeriodBWindowId = b.Window.Id,
            Figures = figures,
            ChangedTextSections = changedTextSections,
            Series = series,
        });
    }
}
