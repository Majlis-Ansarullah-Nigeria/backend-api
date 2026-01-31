using ManagementApi.Domain.Common;

namespace ManagementApi.Domain.Entities.Reports;

/// <summary>
/// Represents a flag on a submission for National attention
/// </summary>
public class SubmissionFlag : AuditableEntity
{
    public Guid ReportSubmissionId { get; private set; }
    public Guid FlaggerId { get; private set; }
    public string FlaggerName { get; private set; } = default!;
    public string Reason { get; private set; } = default!;
    public bool IsActive { get; private set; }
    public DateTime FlaggedDate { get; private set; }
    public DateTime? ResolvedDate { get; private set; }
    public Guid? ResolvedById { get; private set; }
    public string? ResolvedByName { get; private set; }
    public string? ResolutionNotes { get; private set; }

    // Navigation properties
    public ReportSubmission ReportSubmission { get; private set; } = default!;

    private SubmissionFlag() { } // EF Core

    public SubmissionFlag(
        Guid reportSubmissionId,
        Guid flaggerId,
        string flaggerName,
        string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Flag reason cannot be empty", nameof(reason));

        if (reason.Length > 500)
            throw new ArgumentException("Flag reason cannot exceed 500 characters", nameof(reason));

        ReportSubmissionId = reportSubmissionId;
        FlaggerId = flaggerId;
        FlaggerName = flaggerName;
        Reason = reason;
        IsActive = true;
        FlaggedDate = DateTime.UtcNow;
    }

    public void Resolve(Guid resolvedById, string resolvedByName, string? resolutionNotes = null)
    {
        if (!IsActive)
            throw new InvalidOperationException("Cannot resolve a flag that is already resolved");

        if (resolutionNotes != null && resolutionNotes.Length > 500)
            throw new ArgumentException("Resolution notes cannot exceed 500 characters", nameof(resolutionNotes));

        IsActive = false;
        ResolvedDate = DateTime.UtcNow;
        ResolvedById = resolvedById;
        ResolvedByName = resolvedByName;
        ResolutionNotes = resolutionNotes;
    }

    public void UpdateReason(string newReason)
    {
        if (!IsActive)
            throw new InvalidOperationException("Cannot update reason of a resolved flag");

        if (string.IsNullOrWhiteSpace(newReason))
            throw new ArgumentException("Flag reason cannot be empty", nameof(newReason));

        if (newReason.Length > 500)
            throw new ArgumentException("Flag reason cannot exceed 500 characters", nameof(newReason));

        Reason = newReason;
    }
}
