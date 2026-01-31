using FluentValidation;
using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Application.Common.Models;
using ManagementApi.Shared;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ManagementApi.Application.Reports.Commands;

public record DeleteFileCommand(Guid FileAttachmentId) : IRequest<Result>;

public class DeleteFileCommandValidator : AbstractValidator<DeleteFileCommand>
{
    public DeleteFileCommandValidator()
    {
        RuleFor(x => x.FileAttachmentId)
            .NotEmpty().WithMessage("File attachment ID is required");
    }
}

public class DeleteFileCommandHandler : IRequestHandler<DeleteFileCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public DeleteFileCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(DeleteFileCommand request, CancellationToken cancellationToken)
    {
        // Get the file attachment with its submission
        var attachment = await _context.FileAttachments
            .Include(f => f.ReportSubmission)
            .FirstOrDefaultAsync(f => f.Id == request.FileAttachmentId, cancellationToken);

        if (attachment == null)
        {
            return Result.Failure("File attachment not found");
        }

        // Verify user has permission to delete this file
        var userId = _currentUser.UserId;
        if (attachment.ReportSubmission.SubmitterId != userId)
        {
            return Result.Failure("You do not have permission to delete this file");
        }

        // Verify submission is in Draft status (can only delete from draft submissions)
        if (attachment.ReportSubmission.Status != Domain.Enums.SubmissionStatus.Draft)
        {
            return Result.Failure("Files can only be deleted from draft submissions");
        }

        // Remove the file attachment
        attachment.ReportSubmission.RemoveFileAttachment(request.FileAttachmentId);

        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
