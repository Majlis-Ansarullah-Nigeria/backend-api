using System.Globalization;
using System.Text.Json;
using HRS.Application.Common.Interfaces;
using HRS.Application.Features.Submissions.Attachments;
using HRS.Application.Features.Templates.TemplateVersions;
using HRS.Domain.Common;
using HRS.Domain.Templates;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Submissions.Review;

/// <summary>FR-SUB-006's "warnings (for example a figure 50% below last month)" — compares every
/// numeric field against the unit's most recent PRIOR submission of the same report type,
/// regardless of exact period adjacency (windows aren't always monthly), and flags a >=50%
/// swing either direction.</summary>
public sealed class GetReportReviewWarningsQueryHandler(IAppDbContext db, IAccessPolicy accessPolicy, IClock clock)
    : IRequestHandler<GetReportReviewWarningsQuery, Result<ReviewWarningsResult>>
{
    private const double WarningThreshold = 0.5;

    private static readonly TemplateFieldType[] NumericTypes =
        [TemplateFieldType.Number, TemplateFieldType.Currency, TemplateFieldType.Percentage];

    public async ValueTask<Result<ReviewWarningsResult>> Handle(GetReportReviewWarningsQuery request, CancellationToken cancellationToken)
    {
        if (!await accessPolicy.CanEditReportForUnitAsync(request.UserId, request.UnitId, cancellationToken))
        {
            return Result.Failure<ReviewWarningsResult>(Error.Forbidden("FORBIDDEN", "You don't have access to this unit's reports."));
        }

        var instanceResult = await ExistingReportResolver.ResolveAsync(db, request.ReportId, request.UnitId, clock.UtcNow, cancellationToken);
        if (instanceResult.IsFailure)
        {
            return Result.Failure<ReviewWarningsResult>(instanceResult.Error);
        }

        var instance = instanceResult.Value;
        var window = await db.SubmissionWindows.AsNoTracking().FirstAsync(w => w.Id == instance.WindowId, cancellationToken);

        var previous = await db.ReportInstances.AsNoTracking()
            .Join(db.SubmissionWindows.AsNoTracking(), i => i.WindowId, w => w.Id, (i, w) => new { Instance = i, Window = w })
            .Where(x => x.Instance.UnitId == request.UnitId
                && x.Window.ReportTypeId == window.ReportTypeId
                && x.Instance.WindowId != instance.WindowId
                && x.Instance.SubmittedAt != null)
            .OrderByDescending(x => x.Instance.SubmittedAt)
            .Select(x => x.Instance)
            .FirstOrDefaultAsync(cancellationToken);

        if (previous is null)
        {
            return new ReviewWarningsResult([]);
        }

        var templateVersion = await db.TemplateVersions.AsNoTracking().FirstAsync(v => v.Id == instance.TemplateVersionId, cancellationToken);
        var schema = JsonSerializer.Deserialize<TemplateSchema>(templateVersion.SchemaJson, JsonSerializerOptions.Web)!;

        var currentAnswers = JsonSerializer.Deserialize<Dictionary<string, object?>>(instance.AnswersJson, JsonSerializerOptions.Web) ?? [];
        var previousAnswers = JsonSerializer.Deserialize<Dictionary<string, object?>>(previous.AnswersJson, JsonSerializerOptions.Web) ?? [];

        var warnings = new List<string>();
        foreach (var field in schema.Sections.SelectMany(s => s.Fields).Where(f => NumericTypes.Contains(f.Type)))
        {
            var current = AnswerValue.ToDoubleOrNull(currentAnswers.GetValueOrDefault(field.Key));
            var previousValue = AnswerValue.ToDoubleOrNull(previousAnswers.GetValueOrDefault(field.Key));

            if (current is null || previousValue is null || previousValue == 0)
            {
                continue;
            }

            var change = (current.Value - previousValue.Value) / Math.Abs(previousValue.Value);
            if (Math.Abs(change) < WarningThreshold)
            {
                continue;
            }

            var direction = change > 0 ? "higher" : "lower";
            var percent = Math.Abs(change * 100).ToString("0", CultureInfo.InvariantCulture);
            var previousText = previousValue.Value.ToString("0.##", CultureInfo.InvariantCulture);
            warnings.Add($"{field.Label} is {percent}% {direction} than last submission ({previousText}).");
        }

        return new ReviewWarningsResult(warnings);
    }
}
