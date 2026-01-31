using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Application.Common.Models;
using ManagementApi.Application.Reports.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ManagementApi.Application.Reports.Queries;

public record GetUserNotificationsQuery(
    int PageNumber = 1,
    int PageSize = 20,
    bool? IsRead = null,
    string? Type = null
) : IRequest<Result<PaginationResponse<NotificationDto>>>;

public class GetUserNotificationsQueryHandler : IRequestHandler<GetUserNotificationsQuery, Result<PaginationResponse<NotificationDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUser _currentUser;

    public GetUserNotificationsQueryHandler(
        IApplicationDbContext context,
        ICurrentUser currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<PaginationResponse<NotificationDto>>> Handle(
        GetUserNotificationsQuery query,
        CancellationToken cancellationToken)
    {
        var currentUserId = _currentUser.GetUserId();

        var notificationsQuery = _context.Notifications
            .Where(n => n.RecipientId == currentUserId);

        // Apply filters
        if (query.IsRead.HasValue)
        {
            notificationsQuery = notificationsQuery.Where(n => n.IsRead == query.IsRead.Value);
        }

        if (!string.IsNullOrEmpty(query.Type))
        {
            if (Enum.TryParse<Domain.Entities.Reports.NotificationType>(query.Type, true, out var notificationType))
            {
                notificationsQuery = notificationsQuery.Where(n => n.Type == notificationType);
            }
        }

        // Order by most recent first, with unread prioritized
        notificationsQuery = notificationsQuery
            .OrderByDescending(n => !n.IsRead)
            .ThenByDescending(n => n.CreatedOn);

        var totalCount = await notificationsQuery.CountAsync(cancellationToken);

        var notifications = await notificationsQuery
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        var notificationDtos = notifications.Select(NotificationDto.FromEntity).ToList();

        var response = new PaginationResponse<NotificationDto>
        {
            Data = notificationDtos,
            TotalCount = totalCount,
            PageNumber = query.PageNumber,
            PageSize = query.PageSize
        };

        return Result<PaginationResponse<NotificationDto>>.Success(response);
    }
}
