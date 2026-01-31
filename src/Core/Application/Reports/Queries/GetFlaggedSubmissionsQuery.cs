using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Application.Common.Models;
using ManagementApi.Application.Reports.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ManagementApi.Application.Reports.Queries;

public record GetFlaggedSubmissionsQuery(
    bool OnlyActive = true,
    int PageNumber = 1,
    int PageSize = 20
) : IRequest<Result<PaginationResponse<SubmissionFlagDto>>>;

public class GetFlaggedSubmissionsQueryHandler : IRequestHandler<GetFlaggedSubmissionsQuery, Result<PaginationResponse<SubmissionFlagDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetFlaggedSubmissionsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<PaginationResponse<SubmissionFlagDto>>> Handle(
        GetFlaggedSubmissionsQuery query,
        CancellationToken cancellationToken)
    {
        var flagsQuery = _context.SubmissionFlags
            .Include(f => f.ReportSubmission)
            .ThenInclude(s => s.ReportTemplate)
            .AsQueryable();

        // Apply filters
        if (query.OnlyActive)
        {
            flagsQuery = flagsQuery.Where(f => f.IsActive);
        }

        // Order by most recent first, with active flags prioritized
        flagsQuery = flagsQuery
            .OrderByDescending(f => f.IsActive)
            .ThenByDescending(f => f.FlaggedDate);

        var totalCount = await flagsQuery.CountAsync(cancellationToken);

        var flags = await flagsQuery
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        var flagDtos = flags.Select(SubmissionFlagDto.FromEntity).ToList();

        var response = new PaginationResponse<SubmissionFlagDto>
        {
            Data = flagDtos,
            TotalCount = totalCount,
            PageNumber = query.PageNumber,
            PageSize = query.PageSize
        };

        return Result<PaginationResponse<SubmissionFlagDto>>.Success(response);
    }
}
