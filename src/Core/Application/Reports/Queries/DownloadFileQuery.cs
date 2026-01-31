using FluentValidation;
using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Application.Common.Models;
using ManagementApi.Application.Reports.DTOs;
using ManagementApi.Shared;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ManagementApi.Application.Reports.Queries;

public record DownloadFileQuery(Guid FileAttachmentId) : IRequest<Result<FileDownloadDto>>;

public class DownloadFileQueryValidator : AbstractValidator<DownloadFileQuery>
{
    public DownloadFileQueryValidator()
    {
        RuleFor(x => x.FileAttachmentId)
            .NotEmpty().WithMessage("File attachment ID is required");
    }
}

public class DownloadFileQueryHandler : IRequestHandler<DownloadFileQuery, Result<FileDownloadDto>>
{
    private readonly IApplicationDbContext _context;

    public DownloadFileQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<FileDownloadDto>> Handle(DownloadFileQuery request, CancellationToken cancellationToken)
    {
        var attachment = await _context.FileAttachments
            .FirstOrDefaultAsync(f => f.Id == request.FileAttachmentId, cancellationToken);

        if (attachment == null)
        {
            return Result<FileDownloadDto>.Failure("File attachment not found");
        }

        var fileDto = new FileDownloadDto
        {
            FileName = attachment.FileName,
            ContentType = attachment.ContentType,
            FileData = attachment.FileData
        };

        return Result<FileDownloadDto>.Success(fileDto);
    }
}
