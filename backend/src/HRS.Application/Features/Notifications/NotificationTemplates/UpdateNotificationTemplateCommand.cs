using HRS.Application.Common.Interfaces;
using HRS.Domain.Access;
using HRS.Domain.Common;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Notifications.NotificationTemplates;

public sealed record UpdateNotificationTemplateCommand(
    Guid UserId, Guid TemplateId, string Subject, string Body) : IRequest<Result<NotificationTemplateSummary>>;

public sealed class UpdateNotificationTemplateCommandHandler(IAppDbContext db, IAccessPolicy accessPolicy, IClock clock)
    : IRequestHandler<UpdateNotificationTemplateCommand, Result<NotificationTemplateSummary>>
{
    public async ValueTask<Result<NotificationTemplateSummary>> Handle(
        UpdateNotificationTemplateCommand request, CancellationToken cancellationToken)
    {
        if (!await accessPolicy.HasRoleAsync(request.UserId, RoleName.Secretariat, cancellationToken))
        {
            return Result.Failure<NotificationTemplateSummary>(
                Error.Forbidden("FORBIDDEN", "Only the Secretariat can manage notification templates."));
        }

        if (string.IsNullOrWhiteSpace(request.Subject) || string.IsNullOrWhiteSpace(request.Body))
        {
            return Result.Failure<NotificationTemplateSummary>(
                Error.Validation("SUBJECT_AND_BODY_REQUIRED", "A notification template needs both a subject and a body."));
        }

        var template = await db.NotificationTemplates.FirstOrDefaultAsync(t => t.Id == request.TemplateId, cancellationToken);
        if (template is null)
        {
            return Result.Failure<NotificationTemplateSummary>(Error.NotFound("TEMPLATE_NOT_FOUND", "That notification template doesn't exist."));
        }

        template.Update(request.Subject.Trim(), request.Body.Trim(), request.UserId, clock.UtcNow);
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success(NotificationTemplateSummary.FromEntity(template));
    }
}
