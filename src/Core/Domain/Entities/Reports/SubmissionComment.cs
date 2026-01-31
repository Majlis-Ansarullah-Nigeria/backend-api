using ManagementApi.Domain.Common;

namespace ManagementApi.Domain.Entities.Reports;

/// <summary>
/// Represents a standalone comment on a submission (not tied to approval/rejection)
/// Supports threaded discussions via ParentCommentId
/// </summary>
public class SubmissionComment : AuditableEntity
{
    public Guid ReportSubmissionId { get; private set; }
    public Guid? ParentCommentId { get; private set; } // For threading/replies
    public Guid CommenterId { get; private set; }
    public string CommenterName { get; private set; } = default!;
    public string Content { get; private set; } = default!;
    public bool IsEdited { get; private set; }
    public DateTime? EditedAt { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAt { get; private set; }

    // Navigation properties
    public ReportSubmission ReportSubmission { get; private set; } = default!;
    public SubmissionComment? ParentComment { get; private set; }
    public ICollection<SubmissionComment> Replies { get; private set; } = new List<SubmissionComment>();

    private SubmissionComment() { } // EF Core

    public SubmissionComment(
        Guid reportSubmissionId,
        Guid commenterId,
        string commenterName,
        string content,
        Guid? parentCommentId = null)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Comment content cannot be empty", nameof(content));

        if (content.Length > 2000)
            throw new ArgumentException("Comment content cannot exceed 2000 characters", nameof(content));

        if (string.IsNullOrWhiteSpace(commenterName))
            throw new ArgumentException("Commenter name cannot be empty", nameof(commenterName));

        ReportSubmissionId = reportSubmissionId;
        CommenterId = commenterId;
        CommenterName = commenterName;
        Content = content;
        ParentCommentId = parentCommentId;
        IsEdited = false;
        IsDeleted = false;
    }

    public void UpdateContent(string newContent, Guid editorId)
    {
        if (IsDeleted)
            throw new InvalidOperationException("Cannot edit a deleted comment");

        if (editorId != CommenterId)
            throw new UnauthorizedAccessException("Only the commenter can edit this comment");

        if (string.IsNullOrWhiteSpace(newContent))
            throw new ArgumentException("Comment content cannot be empty", nameof(newContent));

        if (newContent.Length > 2000)
            throw new ArgumentException("Comment content cannot exceed 2000 characters", nameof(newContent));

        Content = newContent;
        IsEdited = true;
        EditedAt = DateTime.UtcNow;
    }

    public void Delete(Guid deleterId)
    {
        if (IsDeleted)
            throw new InvalidOperationException("Comment is already deleted");

        if (deleterId != CommenterId)
            throw new UnauthorizedAccessException("Only the commenter can delete this comment");

        IsDeleted = true;
        DeletedAt = DateTime.UtcNow;
    }

    public void Restore(Guid restorerId)
    {
        if (!IsDeleted)
            throw new InvalidOperationException("Comment is not deleted");

        if (restorerId != CommenterId)
            throw new UnauthorizedAccessException("Only the commenter can restore this comment");

        IsDeleted = false;
        DeletedAt = null;
    }
}
