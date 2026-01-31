using ManagementApi.Domain.Entities.Reports;

namespace ManagementApi.Application.Reports.DTOs;

public record SubmissionFlagDto
{
    public Guid Id { get; init; }
    public Guid ReportSubmissionId { get; init; }
    public Guid FlaggerId { get; init; }
    public string FlaggerName { get; init; } = default!;
    public string Reason { get; init; } = default!;
    public bool IsActive { get; init; }
    public DateTime FlaggedDate { get; init; }
    public DateTime? ResolvedDate { get; init; }
    public Guid? ResolvedById { get; init; }
    public string? ResolvedByName { get; init; }
    public string? ResolutionNotes { get; init; }
    public string? SubmissionTitle { get; init; }
    public string? OrganizationName { get; init; }

    public static SubmissionFlagDto FromEntity(SubmissionFlag flag)
    {
        // Get organization name from submission context
        string? organizationName = null;
        if (flag.ReportSubmission != null)
        {
            // Format organization info based on what's available
            if (flag.ReportSubmission.MuqamId.HasValue)
            {
                organizationName = $"Muqam (ID: {flag.ReportSubmission.MuqamId})";
            }
            else if (flag.ReportSubmission.DilaId.HasValue)
            {
                organizationName = $"Dila (ID: {flag.ReportSubmission.DilaId})";
            }
            else if (flag.ReportSubmission.ZoneId.HasValue)
            {
                organizationName = $"Zone (ID: {flag.ReportSubmission.ZoneId})";
            }
            else
            {
                organizationName = flag.ReportSubmission.OrganizationLevel.ToString();
            }
        }

        return new SubmissionFlagDto
        {
            Id = flag.Id,
            ReportSubmissionId = flag.ReportSubmissionId,
            FlaggerId = flag.FlaggerId,
            FlaggerName = flag.FlaggerName,
            Reason = flag.Reason,
            IsActive = flag.IsActive,
            FlaggedDate = flag.FlaggedDate,
            ResolvedDate = flag.ResolvedDate,
            ResolvedById = flag.ResolvedById,
            ResolvedByName = flag.ResolvedByName,
            ResolutionNotes = flag.ResolutionNotes,
            SubmissionTitle = flag.ReportSubmission?.ReportTemplate?.Name,
            OrganizationName = organizationName
        };
    }
}

public record FlagSubmissionRequest
{
    public Guid SubmissionId { get; init; }
    public string Reason { get; init; } = default!;
}

public record UnflagSubmissionRequest
{
    public Guid FlagId { get; init; }
    public string? ResolutionNotes { get; init; }
}

public record GetFlaggedSubmissionsRequest
{
    public bool OnlyActive { get; init; } = true;
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}
