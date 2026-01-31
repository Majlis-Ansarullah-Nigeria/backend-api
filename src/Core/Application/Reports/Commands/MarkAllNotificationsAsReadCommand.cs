using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ManagementApi.Application.Reports.Commands;

public record MarkAllNotificationsAsReadCommand : IRequest<Result<string>>;

public class MarkAllNotificationsAsReadCommandHandler : IRequestHandler<MarkAllNotificationsAsReadCommand, Result<string>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUser _currentUser;

    public MarkAllNotificationsAsReadCommandHandler(
        IApplicationDbContext context,
        ICurrentUser currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<string>> Handle(MarkAllNotificationsAsReadCommand command, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUser.GetUserId();

        var unreadNotifications = await _context.Notifications
            .Where(n => n.RecipientId == currentUserId && !n.IsRead)
            .ToListAsync(cancellationToken);

        if (!unreadNotifications.Any())
        {
            return Result<string>.Success("No unread notifications");
        }

        foreach (var notification in unreadNotifications)
        {
            notification.MarkAsRead();
        }

        await _context.SaveChangesAsync(cancellationToken);

        return Result<string>.Success($"{unreadNotifications.Count} notification(s) marked as read");
    }
}
