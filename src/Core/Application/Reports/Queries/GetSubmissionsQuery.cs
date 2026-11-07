using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Application.Common.Models;
using ManagementApi.Application.Reports.Common;
using ManagementApi.Application.Reports.DTOs;
using ManagementApi.Domain.Enums;
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
            .Include(s => s.SubmissionWindow!.Extensions)
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

        query = filter.StatusGroup switch
        {
            "ToReview" => query.Where(s => s.Status == SubmissionStatus.Submitted || s.Status == SubmissionStatus.InReview),
            "Returned" => query.Where(s => s.Status == SubmissionStatus.Returned),
            "Acknowledged" => query.Where(s => s.Status == SubmissionStatus.Acknowledged),
            "AllSubmitted" => query.Where(s => s.Status != SubmissionStatus.Draft && s.Status != SubmissionStatus.Missed),
            _ => query
        };

        if (filter.IsLate.HasValue)
        {
            query = query.Where(s => s.IsLate == filter.IsLate.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim().ToLower();
            var muqams = _context.Muqams.Where(m => m.Name.ToLower().Contains(term)).Select(m => m.Id);
            var dilas = _context.Dilas.Where(d => d.Name.ToLower().Contains(term)).Select(d => d.Id);
            var zones = _context.Zones.Where(z => z.Name.ToLower().Contains(term)).Select(z => z.Id);
            query = query.Where(s =>
                (s.ReferenceNumber != null && s.ReferenceNumber.ToLower().Contains(term)) ||
                s.SubmitterName.ToLower().Contains(term) ||
                (s.OrganizationLevel == Domain.Enums.OrganizationLevel.Muqam && muqams.Contains(s.UnitId)) ||
                (s.OrganizationLevel == Domain.Enums.OrganizationLevel.Dila && dilas.Contains(s.UnitId)) ||
                (s.OrganizationLevel == Domain.Enums.OrganizationLevel.Zone && zones.Contains(s.UnitId)));
        }

        if (filter.OnlyOwnUnit == true)
        {
            var level = _currentUser.OrganizationLevel;
            var ownId = level switch
            {
                Domain.Enums.OrganizationLevel.Muqam => _currentUser.MuqamId,
                Domain.Enums.OrganizationLevel.Dila => _currentUser.DilaId,
                Domain.Enums.OrganizationLevel.Zone => _currentUser.ZoneId,
                _ => (Guid?)Guid.Empty
            };
            query = query.Where(s => s.OrganizationLevel == level && s.UnitId == ownId);
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
        var ids = submissions.Select(s => s.Id).ToList();
        var flagged = (await _context.SubmissionFlags
            .Where(f => f.IsActive && ids.Contains(f.ReportSubmissionId))
            .Select(f => f.ReportSubmissionId)
            .ToListAsync(cancellationToken)).ToHashSet();

        var dtos = submissions
            .Select(s => names.Apply(ReportSubmissionDto.From(s, _currentUser), s) with { IsFlagged = flagged.Contains(s.Id) })
            .ToList();

        return Result<PaginationResponse<ReportSubmissionDto>>.Success(
            new PaginationResponse<ReportSubmissionDto>(dtos, totalCount, filter.PageNumber, filter.PageSize));
    }
}
