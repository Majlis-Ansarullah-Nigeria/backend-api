using HRS.Application.Common.Interfaces;
using HRS.Domain.Access;
using HRS.Domain.Common;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Notifications.NotificationTemplates;

/// <summary>FR-NOT-003: all 9 FR-NOT-002 event templates, Secretariat-only, for the
/// "Announcements and email wording" screen's template editor.</summary>
public sealed record GetNotificationTemplatesQuery(Guid UserId) : IRequest<Result<IReadOnlyList<NotificationTemplateSummary>>>;

public sealed class GetNotificationTemplatesQueryHandler(IAppDbContext db, IAccessPolicy accessPolicy)
    : IRequestHandler<GetNotificationTemplatesQuery, Result<IReadOnlyList<NotificationTemplateSummary>>>
{
    public async ValueTask<Result<IReadOnlyList<NotificationTemplateSummary>>> Handle(
        GetNotificationTemplatesQuery request, CancellationToken cancellationToken)
    {
        if (!await accessPolicy.HasRoleAsync(request.UserId, RoleName.Secretariat, cancellationToken))
        {
            return Result.Failure<IReadOnlyList<NotificationTemplateSummary>>(
                Error.Forbidden("FORBIDDEN", "Only the Secretariat can manage notification templates."));
        }

        var templates = await db.NotificationTemplates.AsNoTracking()
            .OrderBy(t => t.EventType)
            .ToListAsync(cancellationToken);

        IReadOnlyList<NotificationTemplateSummary> result = [.. templates.Select(NotificationTemplateSummary.FromEntity)];
        return Result.Success(result);
    }
}
