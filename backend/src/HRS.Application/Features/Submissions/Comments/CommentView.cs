namespace HRS.Application.Features.Submissions.Comments;

public sealed record CommentView(Guid Id, Guid AuthorUserId, string AuthorName, string Body, DateTimeOffset CreatedAt);

/// <summary>FR-REV-003's thread: one field, one root comment, a flat list of replies underneath.</summary>
public sealed record CommentThread(string FieldKey, string FieldLabel, CommentView Root, IReadOnlyList<CommentView> Replies);
