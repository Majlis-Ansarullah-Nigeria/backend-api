using ManagementApi.Domain.Entities.Reports;

namespace ManagementApi.Application.Reports.DTOs;

public record SubmissionCommentDto
{
    public Guid Id { get; init; }
    public Guid ReportSubmissionId { get; init; }
    public Guid? ParentCommentId { get; init; }
    public Guid CommenterId { get; init; }
    public string CommenterName { get; init; } = default!;
    public string Content { get; init; } = default!;
    public bool IsEdited { get; init; }
    public DateTime? EditedAt { get; init; }
    public bool IsDeleted { get; init; }
    public DateTime CreatedOn { get; init; }
    public int RepliesCount { get; init; }
    public List<SubmissionCommentDto>? Replies { get; init; }

    public static SubmissionCommentDto FromEntity(SubmissionComment comment, bool includeReplies = false)
    {
        return new SubmissionCommentDto
        {
            Id = comment.Id,
            ReportSubmissionId = comment.ReportSubmissionId,
            ParentCommentId = comment.ParentCommentId,
            CommenterId = comment.CommenterId,
            CommenterName = comment.CommenterName,
            Content = comment.IsDeleted ? "[This comment has been deleted]" : comment.Content,
            IsEdited = comment.IsEdited,
            EditedAt = comment.EditedAt,
            IsDeleted = comment.IsDeleted,
            CreatedOn = comment.CreatedOn,
            RepliesCount = comment.Replies?.Count ?? 0,
            Replies = includeReplies && comment.Replies != null
                ? comment.Replies
                    .Where(r => !r.IsDeleted)
                    .OrderBy(r => r.CreatedOn)
                    .Select(r => FromEntity(r, false))
                    .ToList()
                : null
        };
    }
}

public record AddCommentRequest
{
    public Guid SubmissionId { get; init; }
    public string Content { get; init; } = default!;
    public Guid? ParentCommentId { get; init; }
}

public record UpdateCommentRequest
{
    public Guid CommentId { get; init; }
    public string Content { get; init; } = default!;
}

public record DeleteCommentRequest
{
    public Guid CommentId { get; init; }
}

public record GetCommentsRequest
{
    public Guid SubmissionId { get; init; }
    public bool IncludeDeleted { get; init; } = false;
    public bool IncludeReplies { get; init; } = true;
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
}
