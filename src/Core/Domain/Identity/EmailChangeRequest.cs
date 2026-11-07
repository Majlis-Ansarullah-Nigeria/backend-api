using ManagementApi.Domain.Common;

namespace ManagementApi.Domain.Identity;

public enum EmailChangeRequestStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3
}

/// <summary>
/// A member who cannot use the email on their account asks for a new one so they can reset their password.
/// The National Secretariat approves it before any reset link goes to the new address, because anyone
/// who knows a membership number could otherwise take over that account.
/// </summary>
public class EmailChangeRequest : AuditableEntity
{
    public Guid UserId { get; private set; }
    public string ChandaNo { get; private set; } = default!;
    public string? CurrentEmail { get; private set; }
    public string RequestedEmail { get; private set; } = default!;
    public string? PhoneNumber { get; private set; }
    public string? Note { get; private set; }
    public EmailChangeRequestStatus Status { get; private set; } = EmailChangeRequestStatus.Pending;
    public DateTime RequestedAt { get; private set; }
    public DateTime? ReviewedAt { get; private set; }
    public Guid? ReviewedById { get; private set; }
    public string? ReviewedByName { get; private set; }
    public string? RejectionReason { get; private set; }

    private EmailChangeRequest() { } // EF Core

    public EmailChangeRequest(Guid userId, string chandaNo, string? currentEmail, string requestedEmail, string? phoneNumber, string? note, DateTime requestedAt)
    {
        UserId = userId;
        ChandaNo = chandaNo;
        CurrentEmail = currentEmail;
        RequestedEmail = requestedEmail.Trim();
        PhoneNumber = string.IsNullOrWhiteSpace(phoneNumber) ? null : phoneNumber.Trim();
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        RequestedAt = requestedAt;
    }

    public void Approve(Guid reviewerId, string reviewerName, DateTime nowUtc)
    {
        EnsurePending();
        Status = EmailChangeRequestStatus.Approved;
        Review(reviewerId, reviewerName, nowUtc);
    }

    public void Reject(Guid reviewerId, string reviewerName, string? reason, DateTime nowUtc)
    {
        EnsurePending();
        Status = EmailChangeRequestStatus.Rejected;
        RejectionReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        Review(reviewerId, reviewerName, nowUtc);
    }

    /// <summary>A newer request from the same member replaces this one.</summary>
    public void Supersede(DateTime nowUtc)
    {
        if (Status == EmailChangeRequestStatus.Pending)
        {
            Status = EmailChangeRequestStatus.Rejected;
            RejectionReason = "Replaced by a newer request";
            ReviewedAt = nowUtc;
        }
    }

    private void EnsurePending()
    {
        if (Status != EmailChangeRequestStatus.Pending)
        {
            throw new InvalidOperationException("This request has already been reviewed.");
        }
    }

    private void Review(Guid reviewerId, string reviewerName, DateTime nowUtc)
    {
        ReviewedById = reviewerId;
        ReviewedByName = reviewerName;
        ReviewedAt = nowUtc;
    }
}
