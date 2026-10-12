using System.Text.Json;
using HRS.Application.Common.Interfaces;
using HRS.Application.Features.Review.Inbox;
using HRS.Application.Features.Templates.TemplateVersions;
using HRS.Domain.Access;
using HRS.Domain.Common;
using HRS.Domain.Submissions;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Submissions.Comments;

/// <summary>FR-REV-003's write side. A root comment (ParentId null) needs a FieldKey that
/// actually exists in the pinned template version; a reply (ParentId set) inherits its root's
/// FieldKey and is rejected if ParentId itself points at a reply rather than a root — threads are
/// two levels, not arbitrarily nested (ReportComment's own doc comment).</summary>
public sealed record AddReportCommentCommand(Guid UserId, Guid ReportId, string? FieldKey, Guid? ParentId, string Body) : IRequest<Result<CommentView>>;

public sealed class AddReportCommentCommandHandler(
    IAppDbContext db, IAccessPolicy accessPolicy, IIdentityService identityService, IEmailSender emailSender, IClock clock)
    : IRequestHandler<AddReportCommentCommand, Result<CommentView>>
{
    public async ValueTask<Result<CommentView>> Handle(AddReportCommentCommand request, CancellationToken cancellationToken)
    {
        var instance = await db.ReportInstances.FirstOrDefaultAsync(i => i.Id == request.ReportId, cancellationToken);
        if (instance is null || instance.Status is ReportInstanceStatus.NotStarted or ReportInstanceStatus.Draft or ReportInstanceStatus.Missed)
        {
            return Result.Failure<CommentView>(Error.NotFound("REPORT_NOT_FOUND", "That report doesn't exist."));
        }

        var isSecretariat = await accessPolicy.HasRoleAsync(request.UserId, RoleName.Secretariat, cancellationToken);
        var canEdit = isSecretariat || await accessPolicy.CanEditReportForUnitAsync(request.UserId, instance.UnitId, cancellationToken);
        if (!canEdit)
        {
            return Result.Failure<CommentView>(Error.Forbidden("FORBIDDEN", "You don't have access to this report's comments."));
        }

        if (string.IsNullOrWhiteSpace(request.Body))
        {
            return Result.Failure<CommentView>(Error.Validation("COMMENT_REQUIRED", "Write something before posting."));
        }

        string fieldKey;
        Guid? parentId;

        if (request.ParentId is { } requestedParentId)
        {
            var parent = await db.ReportComments.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == requestedParentId && c.ReportInstanceId == instance.Id, cancellationToken);
            if (parent is null)
            {
                return Result.Failure<CommentView>(Error.NotFound("COMMENT_NOT_FOUND", "That comment doesn't exist."));
            }

            if (parent.ParentId is not null)
            {
                return Result.Failure<CommentView>(Error.Validation("CANNOT_REPLY_TO_REPLY", "Replies go on the thread's first comment, not another reply."));
            }

            fieldKey = parent.FieldKey;
            parentId = parent.Id;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(request.FieldKey))
            {
                return Result.Failure<CommentView>(Error.Validation("FIELD_KEY_REQUIRED", "A new comment thread needs a field to anchor to."));
            }

            var templateVersion = await db.TemplateVersions.AsNoTracking().FirstAsync(v => v.Id == instance.TemplateVersionId, cancellationToken);
            var schema = JsonSerializer.Deserialize<TemplateSchema>(templateVersion.SchemaJson, JsonSerializerOptions.Web)!;
            var fieldExists = schema.Sections.SelectMany(s => s.Fields).Any(f => f.Key == request.FieldKey);
            if (!fieldExists)
            {
                return Result.Failure<CommentView>(Error.Validation("FIELD_NOT_FOUND", "That field doesn't exist on this report."));
            }

            fieldKey = request.FieldKey;
            parentId = null;
        }

        var now = clock.UtcNow;
        var body = request.Body.Trim();
        var comment = ReportComment.Create(Guid.CreateVersion7(), instance.Id, fieldKey, parentId, request.UserId, body, now);
        db.ReportComments.Add(comment);
        await db.SaveChangesAsync(cancellationToken);

        var author = await identityService.GetUserAsync(request.UserId, cancellationToken);

        var window = await db.SubmissionWindows.AsNoTracking().FirstAsync(w => w.Id == instance.WindowId, cancellationToken);
        var reportType = await db.ReportTypes.AsNoTracking().FirstAsync(r => r.Id == window.ReportTypeId, cancellationToken);
        var unitName = await db.Units.AsNoTracking().Where(u => u.Id == instance.UnitId).Select(u => u.Name).FirstAsync(cancellationToken);
        var subject = $"{reportType.Name} — {window.PeriodLabel}: new comment";
        var notificationBody = $"{author?.Name ?? "Someone"} commented on {unitName}'s {window.PeriodLabel} {reportType.Name}:\n\n{body}";

        if (isSecretariat)
        {
            await ReportNotifier.NotifyUnitHeadsAsync(db, identityService, emailSender, instance.UnitId, now, subject, notificationBody, cancellationToken);
        }
        else if (instance.AssignedToUserId is { } assignedToUserId)
        {
            var assignee = await identityService.GetUserAsync(assignedToUserId, cancellationToken);
            if (assignee is not null)
            {
                await emailSender.QueueAsync(new EmailMessage(assignee.Email, subject, notificationBody), cancellationToken);
            }
        }

        return Result.Success(new CommentView(comment.Id, comment.AuthorUserId, author?.Name ?? "Unknown", comment.Body, comment.CreatedAt));
    }
}
