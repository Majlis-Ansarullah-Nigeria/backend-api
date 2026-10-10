using System.Globalization;
using System.Text.Json;
using HRS.Application.Common.Interfaces;
using HRS.Application.Features.Submissions.ReportInstances;
using HRS.Application.Features.Submissions.Review;
using HRS.Application.Features.Templates.TemplateVersions;
using HRS.Domain.Common;
using HRS.Domain.Submissions;
using HRS.Domain.Templates;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Submissions.PullFigures;

public sealed class PullChildFiguresCommandHandler(IAppDbContext db, IAccessPolicy accessPolicy, IClock clock)
    : IRequestHandler<PullChildFiguresCommand, Result<PullChildFiguresResult>>
{
    private static readonly HashSet<TemplateFieldType> NumericFieldTypes =
        [TemplateFieldType.Number, TemplateFieldType.Currency, TemplateFieldType.Percentage];

    private static readonly HashSet<ReportInstanceStatus> SubmittedStatuses =
        [ReportInstanceStatus.Submitted, ReportInstanceStatus.InReview, ReportInstanceStatus.Returned, ReportInstanceStatus.Acknowledged];

    public async ValueTask<Result<PullChildFiguresResult>> Handle(PullChildFiguresCommand request, CancellationToken cancellationToken)
    {
        if (!await accessPolicy.CanEditReportForUnitAsync(request.UserId, request.UnitId, cancellationToken))
        {
            return Result.Failure<PullChildFiguresResult>(Error.Forbidden("FORBIDDEN", "You don't have access to this unit's reports."));
        }

        var resolved = await PullFiguresResolver.ResolveAsync(db, request.UnitId, request.ReportId, cancellationToken);
        if (resolved is null)
        {
            return Result.Failure<PullChildFiguresResult>(
                Error.Conflict("NOT_ELIGIBLE", "Pull from children isn't available for this report."));
        }

        var unitLevelCode = await db.Units
            .AsNoTracking()
            .Where(u => u.Id == request.UnitId)
            .Join(db.UnitLevels.AsNoTracking(), u => u.LevelId, l => l.Id, (u, l) => l.Code)
            .FirstAsync(cancellationToken);

        var now = clock.UtcNow;
        var editError = ReportEditability.Check(resolved.Window, resolved.Instance, unitLevelCode, request.UnitId, now);
        if (editError is not null)
        {
            return Result.Failure<PullChildFiguresResult>(editError);
        }

        var childUnitIds = resolved.Children.Select(c => c.Id).ToList();
        var childInstances = await db.ReportInstances.AsNoTracking()
            .Where(i => i.WindowId == resolved.Window.Id && childUnitIds.Contains(i.UnitId))
            .ToListAsync(cancellationToken);

        var submittedChildren = childInstances.Where(i => SubmittedStatuses.Contains(i.Status)).ToList();
        var submittedUnitIds = submittedChildren.Select(i => i.UnitId).ToHashSet();
        var missingUnitIds = childUnitIds.Where(id => !submittedUnitIds.Contains(id)).ToList();

        var includedUnitIds = (request.IncludedUnitIds is { Count: > 0 } requested
            ? submittedUnitIds.Intersect(requested)
            : submittedUnitIds).ToHashSet();

        var childNameByUnitId = resolved.Children.ToDictionary(c => c.Id, c => c.Name);
        var includedChildAnswers = submittedChildren
            .Where(i => includedUnitIds.Contains(i.UnitId))
            .Select(i => (Instance: i, Answers: JsonSerializer.Deserialize<Dictionary<string, object?>>(i.AnswersJson, JsonSerializerOptions.Web) ?? []))
            .ToList();

        // Pull against the version this draft is pinned to once one exists (same reasoning as
        // SubmitReportCommandHandler's version resolution); a never-saved draft has nothing
        // pinned yet, so the currently published version is the only sensible source.
        var templateVersion = resolved.Instance is not null
            ? await db.TemplateVersions.AsNoTracking().FirstAsync(v => v.Id == resolved.Instance.TemplateVersionId, cancellationToken)
            : await db.TemplateVersions.AsNoTracking()
                .FirstOrDefaultAsync(v => v.ReportTypeId == resolved.ReportType.Id && v.Status == TemplateVersionStatus.Published, cancellationToken);
        if (templateVersion is null)
        {
            return Result.Failure<PullChildFiguresResult>(
                Error.Conflict("NO_PUBLISHED_TEMPLATE", "This report type has no published template yet."));
        }

        var schema = JsonSerializer.Deserialize<TemplateSchema>(templateVersion.SchemaJson, JsonSerializerOptions.Web)!;
        var numericFields = schema.Sections
            .SelectMany(s => s.Fields)
            .Where(f => NumericFieldTypes.Contains(f.Type) && f.Aggregation != AggregationRule.None)
            .ToList();

        var currentAnswers = resolved.Instance is not null
            ? JsonSerializer.Deserialize<Dictionary<string, object?>>(resolved.Instance.AnswersJson, JsonSerializerOptions.Web) ?? []
            : [];
        var previousSummary = resolved.Instance?.PullSummaryJson is { } previousJson
            ? JsonSerializer.Deserialize<PullSummary>(previousJson, JsonSerializerOptions.Web)
            : null;
        var previousFieldByKey = previousSummary?.Fields.ToDictionary(f => f.FieldKey) ?? [];

        var pulledFields = new List<PullSummaryField>();

        foreach (var field in numericFields)
        {
            var breakdown = includedChildAnswers
                .Select(entry => (entry.Instance.UnitId, Value: AnswerValue.ToDoubleOrNull(entry.Answers.GetValueOrDefault(field.Key))))
                .Where(entry => entry.Value is not null)
                .Select(entry => new PullSummaryFieldBreakdownEntry(entry.UnitId, childNameByUnitId[entry.UnitId], entry.Value!.Value))
                .ToList();

            if (breakdown.Count == 0)
            {
                continue;
            }

            var aggregatedValue = field.Aggregation switch
            {
                AggregationRule.Sum => breakdown.Sum(b => b.Value),
                AggregationRule.Average => breakdown.Average(b => b.Value),
                AggregationRule.Min => breakdown.Min(b => b.Value),
                AggregationRule.Max => breakdown.Max(b => b.Value),
                AggregationRule.Latest => LatestValue(breakdown, includedChildAnswers),
                _ => breakdown.Sum(b => b.Value),
            };

            var currentAnswerValue = AnswerValue.ToDoubleOrNull(currentAnswers.GetValueOrDefault(field.Key));
            var wasManuallyChanged = previousFieldByKey.TryGetValue(field.Key, out var previouslyPulled)
                && currentAnswerValue != previouslyPulled.Value;

            if (!wasManuallyChanged)
            {
                currentAnswers[field.Key] = aggregatedValue;
            }

            pulledFields.Add(new PullSummaryField(field.Key, field.Aggregation, aggregatedValue, wasManuallyChanged, breakdown));
        }

        var summary = new PullSummary(now, [.. includedUnitIds], missingUnitIds, request.OverrideNote, pulledFields);
        var answersJson = JsonSerializer.Serialize(currentAnswers);
        var pullSummaryJson = JsonSerializer.Serialize(summary);

        var instance = resolved.Instance;
        if (instance is null)
        {
            instance = ReportInstance.Create(Guid.CreateVersion7(), resolved.Window.Id, request.UnitId, templateVersion.Id, now);
            instance.RecordPull(answersJson, pullSummaryJson);
            db.ReportInstances.Add(instance);
        }
        else
        {
            instance = await db.ReportInstances.FirstAsync(i => i.Id == instance.Id, cancellationToken);

            var unquoted = request.IfMatch?.Trim('"');
            if (string.IsNullOrEmpty(unquoted) || !uint.TryParse(unquoted, out var claimedVersion))
            {
                return Result.Failure<PullChildFiguresResult>(
                    Error.Validation("IF_MATCH_REQUIRED", "A valid If-Match header is required to pull into an existing report."));
            }

            db.Entry(instance).Property(i => i.RowVersion).OriginalValue = claimedVersion;
            instance.RecordPull(answersJson, pullSummaryJson);
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<PullChildFiguresResult>(Error.Conflict(
                "CONCURRENCY_CONFLICT", "Someone else has saved changes to this report. Reload to see the latest version."));
        }

        return new PullChildFiguresResult(instance.Id, instance.RowVersion.ToString(CultureInfo.InvariantCulture), summary);
    }

    private static double LatestValue(
        List<PullSummaryFieldBreakdownEntry> breakdown,
        List<(ReportInstance Instance, Dictionary<string, object?> Answers)> includedChildAnswers)
    {
        var contributingUnitIds = breakdown.Select(b => b.UnitId).ToHashSet();
        var latestInstance = includedChildAnswers
            .Where(entry => contributingUnitIds.Contains(entry.Instance.UnitId))
            .OrderByDescending(entry => entry.Instance.SubmittedAt)
            .First();

        return breakdown.First(b => b.UnitId == latestInstance.Instance.UnitId).Value;
    }
}
