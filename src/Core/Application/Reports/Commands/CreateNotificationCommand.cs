using FluentValidation;
using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Application.Common.Models;
using ManagementApi.Application.Reports.DTOs;
using ManagementApi.Domain.Entities.Reports;
using MediatR;

namespace ManagementApi.Application.Reports.Commands;

public record CreateNotificationCommand(CreateNotificationRequest Request) : IRequest<Result<Guid>>;

public class CreateNotificationCommandHandler : IRequestHandler<CreateNotificationCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;

    public CreateNotificationCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<Guid>> Handle(CreateNotificationCommand command, CancellationToken cancellationToken)
    {
        var request = command.Request;

        var notification = new Notification(
            request.Type,
            request.RecipientId,
            request.RecipientName,
            request.Title,
            request.Message,
            request.Priority,
            request.RelatedEntityId,
            request.RelatedEntityType
        );

        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(notification.Id, "Notification created successfully");
    }
}

public class CreateNotificationCommandValidator : AbstractValidator<CreateNotificationCommand>
{
    public CreateNotificationCommandValidator()
    {
        RuleFor(x => x.Request.RecipientId)
            .NotEmpty().WithMessage("Recipient ID is required");

        RuleFor(x => x.Request.RecipientName)
            .NotEmpty().WithMessage("Recipient name is required")
            .MaximumLength(200).WithMessage("Recipient name must not exceed 200 characters");

        RuleFor(x => x.Request.Title)
            .NotEmpty().WithMessage("Title is required")
            .MaximumLength(200).WithMessage("Title must not exceed 200 characters");

        RuleFor(x => x.Request.Message)
            .NotEmpty().WithMessage("Message is required")
            .MaximumLength(1000).WithMessage("Message must not exceed 1000 characters");

        RuleFor(x => x.Request.Type)
            .IsInEnum().WithMessage("Invalid notification type");

        RuleFor(x => x.Request.Priority)
            .IsInEnum().WithMessage("Invalid priority level");

        RuleFor(x => x.Request.RelatedEntityType)
            .MaximumLength(100).WithMessage("Related entity type must not exceed 100 characters")
            .When(x => !string.IsNullOrEmpty(x.Request.RelatedEntityType));
    }
}
