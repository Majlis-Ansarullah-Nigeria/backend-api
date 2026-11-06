using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Application.Common.Models;
using ManagementApi.Application.Reports.Common;
using ManagementApi.Application.Reports.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ManagementApi.Application.Reports.Queries;

public record GetSubmissionsQuery(GetSubmissionsRequest Request) : IRequest<Result<PaginationResponse<ReportSubmissionDto>>>;

public class GetSubmissionsQueryHandler : IRequestHandler<GetSubmissionsQuery, Result<PaginationResponse<ReportSubmissionDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetSubmissionsQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<PaginationResponse<ReportSubmissionDto>>> Handle(GetSubmissionsQuery request, CancellationToken cancellationToken)
    {
        // SECURITY: scope first, so request filters can only narrow what the user may see.
        var query = _context.ReportSubmissions
            .Include(s => s.ReportTemplate)
            .Include(s => s.Approvals)
            .AsNoTracking()
            .VisibleTo(_currentUser);

        var filter = request.Request;

        if (filter.ReportTemplateId.HasValue)
        {
            query = query.Where(s => s.ReportTemplateId == filter.ReportTemplateId.Value);
        }

        if (filter.SubmissionWindowId.HasValue)
        {
            query = query.Where(s => s.SubmissionWindowId == filter.SubmissionWindowId.Value);
        }

        if (filter.Status.HasValue)
        {
            query = query.Where(s => s.Status == filter.Status.Value);
        }

        if (filter.IsLate.HasValue)
        {
            query = query.Where(s => s.IsLate == filter.IsLate.Value);
        }

        if (filter.MuqamId.HasValue)
        {
            query = query.Where(s => s.MuqamId == filter.MuqamId.Value);
        }

        if (filter.DilaId.HasValue)
        {
            query = query.Where(s => s.DilaId == filter.DilaId.Value);
        }

        if (filter.ZoneId.HasValue)
        {
            query = query.Where(s => s.ZoneId == filter.ZoneId.Value);
        }

        if (filter.OrganizationLevel.HasValue)
        {
            query = query.Where(s => s.OrganizationLevel == filter.OrganizationLevel.Value);
        }

        if (filter.StartDate.HasValue)
        {
            query = query.Where(s => s.SubmittedAt >= filter.StartDate.Value);
        }

        if (filter.EndDate.HasValue)
        {
            query = query.Where(s => s.SubmittedAt <= filter.EndDate.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var submissions = await query
            .OrderByDescending(s => s.SubmittedAt ?? s.CreatedOn)
            .Skip((filter.PageNumber - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken);

        var names = await UnitNames.LoadAsync(_context, submissions, cancellationToken);

        var dtos = submissions
            .Select(s => names.Apply(ReportSubmissionDto.From(s, _currentUser), s))
            .ToList();

        return Result<PaginationResponse<ReportSubmissionDto>>.Success(
            new PaginationResponse<ReportSubmissionDto>(dtos, totalCount, filter.PageNumber, filter.PageSize));
    }
}
