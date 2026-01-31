using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Application.Common.Models;
using ManagementApi.Application.Reports.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ManagementApi.Application.Reports.Queries;

public record GetUnreadNotificationCountQuery : IRequest<Result<NotificationStatsDto>>;

public class GetUnreadNotificationCountQueryHandler : IRequestHandler<GetUnreadNotificationCountQuery, Result<NotificationStatsDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUser _currentUser;

    public GetUnreadNotificationCountQueryHandler(
        IApplicationDbContext context,
        ICurrentUser currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<NotificationStatsDto>> Handle(
        GetUnreadNotificationCountQuery query,
        CancellationToken cancellationToken)
    {
        var currentUserId = _currentUser.GetUserId();

        var notifications = await _context.Notifications
            .Where(n => n.RecipientId == currentUserId)
            .ToListAsync(cancellationToken);

        var stats = new NotificationStatsDto
        {
            TotalCount = notifications.Count,
            UnreadCount = notifications.Count(n => !n.IsRead),
            ReadCount = notifications.Count(n => n.IsRead),
            LastNotificationDate = notifications.Any()
                ? notifications.Max(n => n.CreatedOn)
                : (DateTime?)null
        };

        return Result<NotificationStatsDto>.Success(stats);
    }
}
