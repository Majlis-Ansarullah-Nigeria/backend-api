using FluentValidation;
using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Application.Common.Models;
using ManagementApi.Application.Reports.DTOs;
using ManagementApi.Shared;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ManagementApi.Application.Reports.Queries;

public record GetFileAttachmentsQuery(Guid SubmissionId) : IRequest<Result<List<FileAttachmentDto>>>;

public class GetFileAttachmentsQueryValidator : AbstractValidator<GetFileAttachmentsQuery>
{
    public GetFileAttachmentsQueryValidator()
    {
        RuleFor(x => x.SubmissionId)
            .NotEmpty().WithMessage("Submission ID is required");
    }
}

public class GetFileAttachmentsQueryHandler : IRequestHandler<GetFileAttachmentsQuery, Result<List<FileAttachmentDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetFileAttachmentsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<FileAttachmentDto>>> Handle(GetFileAttachmentsQuery request, CancellationToken cancellationToken)
    {
        var attachments = await _context.FileAttachments
            .Where(f => f.ReportSubmissionId == request.SubmissionId)
            .OrderBy(f => f.CreatedOn)
            .Select(f => new FileAttachmentDto
            {
                Id = f.Id,
                ReportSubmissionId = f.ReportSubmissionId,
                QuestionId = f.QuestionId,
                FileName = f.FileName,
                ContentType = f.ContentType,
                FileSize = f.FileSize,
                Description = f.Description,
                CreatedOn = f.CreatedOn
            })
            .ToListAsync(cancellationToken);

        return Result<List<FileAttachmentDto>>.Success(attachments);
    }
}
