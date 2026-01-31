using FluentValidation;
using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ManagementApi.Application.Reports.Commands;

public record MarkNotificationAsReadCommand(Guid NotificationId) : IRequest<Result<string>>;

public class MarkNotificationAsReadCommandHandler : IRequestHandler<MarkNotificationAsReadCommand, Result<string>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUser _currentUser;

    public MarkNotificationAsReadCommandHandler(
        IApplicationDbContext context,
        ICurrentUser currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<string>> Handle(MarkNotificationAsReadCommand command, CancellationToken cancellationToken)
    {
        var notification = await _context.Notifications
            .FirstOrDefaultAsync(n => n.Id == command.NotificationId, cancellationToken);

        if (notification == null)
        {
            return Result<string>.Failure("Notification not found");
        }

        // Verify the notification belongs to the current user
        var currentUserId = _currentUser.GetUserId();
        if (notification.RecipientId != currentUserId)
        {
            return Result<string>.Failure("You can only mark your own notifications as read");
        }

        notification.MarkAsRead();
        await _context.SaveChangesAsync(cancellationToken);

        return Result<string>.Success("Notification marked as read");
    }
}

public class MarkNotificationAsReadCommandValidator : AbstractValidator<MarkNotificationAsReadCommand>
{
    public MarkNotificationAsReadCommandValidator()
    {
        RuleFor(x => x.NotificationId)
            .NotEmpty().WithMessage("Notification ID is required");
    }
}
