using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Application.Common.Models;
using ManagementApi.Application.Reports.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ManagementApi.Application.Reports.Queries;

public record GetCommentsQuery(
    Guid SubmissionId,
    bool IncludeDeleted = false,
    bool IncludeReplies = true,
    int PageNumber = 1,
    int PageSize = 50
) : IRequest<Result<PaginationResponse<SubmissionCommentDto>>>;

public class GetCommentsQueryHandler : IRequestHandler<GetCommentsQuery, Result<PaginationResponse<SubmissionCommentDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetCommentsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<PaginationResponse<SubmissionCommentDto>>> Handle(
        GetCommentsQuery query,
        CancellationToken cancellationToken)
    {
        // Verify submission exists
        var submissionExists = await _context.ReportSubmissions
            .AnyAsync(s => s.Id == query.SubmissionId, cancellationToken);

        if (!submissionExists)
        {
            return Result<PaginationResponse<SubmissionCommentDto>>.Failure("Submission not found");
        }

        // Get top-level comments (those without a parent)
        var commentsQuery = _context.SubmissionComments
            .Where(c => c.ReportSubmissionId == query.SubmissionId && c.ParentCommentId == null);

        // Apply filters
        if (!query.IncludeDeleted)
        {
            commentsQuery = commentsQuery.Where(c => !c.IsDeleted);
        }

        // Include replies if requested
        if (query.IncludeReplies)
        {
            commentsQuery = commentsQuery.Include(c => c.Replies.Where(r => query.IncludeDeleted || !r.IsDeleted));
        }

        // Order by creation date (newest first)
        commentsQuery = commentsQuery.OrderByDescending(c => c.CreatedOn);

        var totalCount = await commentsQuery.CountAsync(cancellationToken);

        var comments = await commentsQuery
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        var commentDtos = comments
            .Select(c => SubmissionCommentDto.FromEntity(c, query.IncludeReplies))
            .ToList();

        var response = new PaginationResponse<SubmissionCommentDto>
        {
            Data = commentDtos,
            TotalCount = totalCount,
            PageNumber = query.PageNumber,
            PageSize = query.PageSize
        };

        return Result<PaginationResponse<SubmissionCommentDto>>.Success(response);
    }
}
