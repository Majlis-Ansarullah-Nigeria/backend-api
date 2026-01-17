using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Application.Reports.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ManagementApi.Application.Reports.Queries;

public record GetSubmissionWindowsQuery(Guid? ReportTemplateId = null) : IRequest<List<SubmissionWindowDto>>;

public class GetSubmissionWindowsQueryHandler : IRequestHandler<GetSubmissionWindowsQuery, List<SubmissionWindowDto>>
{
    private readonly IApplicationDbContext _context;

    public GetSubmissionWindowsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<SubmissionWindowDto>> Handle(GetSubmissionWindowsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.SubmissionWindows
            .Include(w => w.ReportTemplate)
            .AsQueryable();

        if (request.ReportTemplateId.HasValue)
        {
            query = query.Where(w => w.ReportTemplateId == request.ReportTemplateId.Value);
        }

        var windows = await query
            .OrderByDescending(w => w.CreatedOn)
            .Select(w => new SubmissionWindowDto
            {
                Id = w.Id,
                ReportTemplateId = w.ReportTemplateId,
                ReportTemplateName = w.ReportTemplate != null ? w.ReportTemplate.Name : "",
                Name = w.Name,
                Description = w.Description,
                StartDate = w.StartDate,
                EndDate = w.EndDate,
                IsActive = w.IsActive,
                IsOpen = w.IsActive && w.StartDate <= DateTime.UtcNow && DateTime.UtcNow <= w.EndDate,
                SubmissionCount = _context.ReportSubmissions.Count(s => s.ReportTemplateId == w.ReportTemplateId &&
                                                                        s.SubmittedAt >= w.StartDate &&
                                                                        s.SubmittedAt <= w.EndDate),
                CreatedOn = w.CreatedOn
            })
            .ToListAsync(cancellationToken);

        return windows;
    }
}
