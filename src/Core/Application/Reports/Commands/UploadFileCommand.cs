using FluentValidation;
using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Application.Common.Models;
using ManagementApi.Application.Reports.DTOs;
using ManagementApi.Shared;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ManagementApi.Application.Reports.Commands;

public record UploadFileCommand(UploadFileRequest Request) : IRequest<Result<Guid>>;

public class UploadFileCommandValidator : AbstractValidator<UploadFileCommand>
{
    public UploadFileCommandValidator()
    {
        RuleFor(x => x.Request.SubmissionId)
            .NotEmpty().WithMessage("Submission ID is required");

        RuleFor(x => x.Request.QuestionId)
            .NotEmpty().WithMessage("Question ID is required");

        RuleFor(x => x.Request.FileName)
            .NotEmpty().WithMessage("File name is required")
            .MaximumLength(255).WithMessage("File name cannot exceed 255 characters");

        RuleFor(x => x.Request.ContentType)
            .NotEmpty().WithMessage("Content type is required");

        RuleFor(x => x.Request.FileData)
            .NotEmpty().WithMessage("File data is required")
            .Must(data => data.Length <= 10 * 1024 * 1024)
            .WithMessage("File size cannot exceed 10MB");

        RuleFor(x => x.Request.Description)
            .MaximumLength(500).WithMessage("Description cannot exceed 500 characters");
    }
}

public class UploadFileCommandHandler : IRequestHandler<UploadFileCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public UploadFileCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<Guid>> Handle(UploadFileCommand request, CancellationToken cancellationToken)
    {
        // Get the submission
        var submission = await _context.ReportSubmissions
            .Include(s => s.FileAttachments)
            .FirstOrDefaultAsync(s => s.Id == request.Request.SubmissionId, cancellationToken);

        if (submission == null)
        {
            return Result<Guid>.Failure("Submission not found");
        }

        // Verify user has permission to upload files to this submission
        var userId = _currentUser.UserId;
        if (submission.SubmitterId != userId)
        {
            return Result<Guid>.Failure("You do not have permission to upload files to this submission");
        }

        // Verify submission is in Draft or Rejected status (can only upload to editable submissions)
        if (submission.Status != Domain.Enums.SubmissionStatus.Draft)
        {
            return Result<Guid>.Failure("Files can only be uploaded to draft submissions");
        }

        // Add the file attachment
        submission.AddFileAttachment(
            request.Request.QuestionId,
            request.Request.FileName,
            request.Request.ContentType,
            request.Request.FileData,
            request.Request.Description);

        await _context.SaveChangesAsync(cancellationToken);

        var attachmentId = submission.FileAttachments.Last().Id;

        return Result<Guid>.Success(attachmentId);
    }
}
