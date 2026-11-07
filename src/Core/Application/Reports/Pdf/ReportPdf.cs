using System.Globalization;
using System.Text.Json;
using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Application.Common.Models;
using ManagementApi.Application.Reports.Queries;
using ManagementApi.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ManagementApi.Application.Reports.Pdf;

// A submitted report as a PDF. Who may download it is exactly who may open the report
// (GetSubmissionByIdQuery decides). Review comments only go to those who may see them.

public record ReportPdfAnswer(int Number, string Question, string? Answer, bool IsRequired);

public record ReportPdfSection(int Number, string Title, string? Description, List<ReportPdfAnswer> Answers);

public record ReportPdfEvent(DateTime AtUtc, string Who, string What, string? Note);

public record ReportPdfModel
{
    public string Title { get; init; } = default!;
    public int TemplateVersion { get; init; }
    public string? WindowName { get; init; }
    public string? ReferenceNumber { get; init; }
    public string Status { get; init; } = default!;
    public bool IsLate { get; init; }
    public string Level { get; init; } = default!;
    public string UnitName { get; init; } = default!;
    public string? ParentUnits { get; init; }
    public string SubmittedBy { get; init; } = default!;
    public DateTime? SubmittedAtUtc { get; init; }
    public DateTime? DueAtUtc { get; init; }
    public DateTime? AcknowledgedAtUtc { get; init; }
    public DateTime? ReturnedAtUtc { get; init; }
    public string? ReturnReason { get; init; }
    public List<ReportPdfSection> Sections { get; init; } = new();
    public List<ReportPdfEvent> History { get; init; } = new();
    public DateTime GeneratedAtUtc { get; init; }
    public int AnsweredCount => Sections.Sum(s => s.Answers.Count(a => !string.IsNullOrWhiteSpace(a.Answer)));
    public int QuestionCount => Sections.Sum(s => s.Answers.Count);
}

public interface IReportPdfRenderer
{
    byte[] Render(ReportPdfModel model);
}

public record ReportPdfFile(string FileName, byte[] Content);

public record GetSubmissionPdfQuery(Guid SubmissionId) : IRequest<Result<ReportPdfFile>>;

public class GetSubmissionPdfQueryHandler : IRequestHandler<GetSubmissionPdfQuery, Result<ReportPdfFile>>
{
    private readonly ISender _mediator;
    private readonly IApplicationDbContext _context;
    private readonly IReportPdfRenderer _renderer;
    private readonly TimeProvider _clock;

    public GetSubmissionPdfQueryHandler(ISender mediator, IApplicationDbContext context, IReportPdfRenderer renderer, TimeProvider clock)
    {
        _mediator = mediator;
        _context = context;
        _renderer = renderer;
        _clock = clock;
    }

    public async Task<Result<ReportPdfFile>> Handle(GetSubmissionPdfQuery request, CancellationToken cancellationToken)
    {
        // Same access rule as opening the report.
        var found = await _mediator.Send(new GetSubmissionByIdQuery(request.SubmissionId), cancellationToken);
        if (!found.Succeeded || found.Data is null)
        {
            return Result<ReportPdfFile>.Failure(found.Messages);
        }

        var report = found.Data;
        var sections = await _context.ReportSections.AsNoTracking()
            .Where(s => s.ReportTemplateId == report.ReportTemplateId)
            .Include(s => s.Questions)
            .OrderBy(s => s.DisplayOrder)
            .ToListAsync(cancellationToken);
        var answers = ParseAnswers(report.ResponseData);

        var questionNumber = 0;
        var model = new ReportPdfModel
        {
            Title = report.ReportTemplateName,
            TemplateVersion = report.ReportTemplateVersion,
            WindowName = report.WindowName,
            ReferenceNumber = report.ReferenceNumber,
            Status = report.Status,
            IsLate = report.IsLate,
            Level = report.OrganizationLevel,
            UnitName = UnitName(report.OrganizationLevel, report.MuqamName, report.DilaName, report.ZoneName),
            ParentUnits = ParentUnits(report.OrganizationLevel, report.DilaName, report.ZoneName),
            SubmittedBy = report.SubmitterName,
            SubmittedAtUtc = report.SubmittedAt,
            DueAtUtc = report.DueAt,
            AcknowledgedAtUtc = report.AcknowledgedAt,
            ReturnedAtUtc = report.ReturnedAt,
            // Observers never see review comments.
            ReturnReason = report.CanComment ? report.ReturnReason : null,
            Sections = sections.Select((s, i) => new ReportPdfSection(
                i + 1,
                s.Title,
                s.Description,
                s.Questions.OrderBy(q => q.DisplayOrder).Select(q => new ReportPdfAnswer(
                    ++questionNumber,
                    q.QuestionText,
                    answers.TryGetValue(q.Id.ToString(), out var value) ? Format(q.QuestionType, value) : null,
                    q.IsRequired)).ToList())).ToList(),
            History = report.CanComment
                ? report.Approvals.OrderBy(a => a.ActionDate).Select(a => new ReportPdfEvent(a.ActionDate, a.ApproverName, a.Status, a.Comments)).ToList()
                : new(),
            GeneratedAtUtc = _clock.GetUtcNow().UtcDateTime,
        };

        var name = (report.ReferenceNumber ?? $"{report.ReportTemplateName}-{model.UnitName}").Replace(' ', '-');
        var safe = new string(name.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_').ToArray());
        return Result<ReportPdfFile>.Success(new ReportPdfFile($"{safe}.pdf", _renderer.Render(model)));
    }

    private static string UnitName(string level, string? muqam, string? dila, string? zone) => level switch
    {
        nameof(OrganizationLevel.Muqam) => $"{muqam} Muqam",
        nameof(OrganizationLevel.Dila) => $"{dila} Dila",
        nameof(OrganizationLevel.Zone) => $"{zone} Zone",
        _ => "National",
    };

    private static string? ParentUnits(string level, string? dila, string? zone) => level switch
    {
        nameof(OrganizationLevel.Muqam) => string.Join(" · ", new[] { dila is null ? null : $"{dila} Dila", zone is null ? null : $"{zone} Zone" }.Where(x => x != null)),
        nameof(OrganizationLevel.Dila) => zone is null ? null : $"{zone} Zone",
        _ => null,
    };

    private static Dictionary<string, JsonElement> ParseAnswers(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new();
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json) ?? new();
        }
        catch (JsonException)
        {
            return new();
        }
    }

    /// <summary>An answer as a reader would write it. Empty answers come back as null.</summary>
    public static string? Format(QuestionType type, JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Null or JsonValueKind.Undefined:
                return null;
            case JsonValueKind.True:
                return "Yes";
            case JsonValueKind.False:
                return "No";
            case JsonValueKind.Number:
                return value.TryGetInt64(out var whole)
                    ? whole.ToString("N0", CultureInfo.InvariantCulture)
                    : value.GetDecimal().ToString("#,0.##", CultureInfo.InvariantCulture);
            case JsonValueKind.Array:
                var items = value.EnumerateArray().Select(v => Format(type, v)).Where(v => !string.IsNullOrWhiteSpace(v)).ToList();
                return items.Count == 0 ? null : type == QuestionType.File ? $"{items.Count} file{(items.Count == 1 ? "" : "s")} attached" : string.Join(", ", items);
            case JsonValueKind.Object:
                return type == QuestionType.File ? "File attached"
                    : value.TryGetProperty("name", out var n) ? n.ToString() : value.ToString();
        }

        var text = value.GetString()?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        if (type == QuestionType.YesNo)
        {
            return text.Equals("true", StringComparison.OrdinalIgnoreCase) || text.Equals("yes", StringComparison.OrdinalIgnoreCase) ? "Yes"
                : text.Equals("false", StringComparison.OrdinalIgnoreCase) || text.Equals("no", StringComparison.OrdinalIgnoreCase) ? "No" : text;
        }

        if (type == QuestionType.Date && DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var date))
        {
            return date.ToString("d MMM yyyy", CultureInfo.InvariantCulture);
        }

        return type == QuestionType.File ? "File attached" : text;
    }
}
