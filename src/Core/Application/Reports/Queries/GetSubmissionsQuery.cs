using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Application.Common.Models;
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
        var query = _context.ReportSubmissions
            .Include(s => s.ReportTemplate)
            .Include(s => s.Approvals)
            .AsQueryable();

        // SECURITY: First apply automatic filtering based on current user's organization level
        // This ensures users can only see submissions within their organizational scope
        var orgLevel = _currentUser.OrganizationLevel;

        switch (orgLevel)
        {
            case ManagementApi.Domain.Enums.OrganizationLevel.Muqam:
                if (_currentUser.MuqamId.HasValue)
                {
                    query = query.Where(s => s.MuqamId == _currentUser.MuqamId.Value);
                }
                break;

            case ManagementApi.Domain.Enums.OrganizationLevel.Dila:
                if (_currentUser.DilaId.HasValue)
                {
                    query = query.Where(s => s.DilaId == _currentUser.DilaId.Value);
                }
                break;

            case ManagementApi.Domain.Enums.OrganizationLevel.Zone:
                if (_currentUser.ZoneId.HasValue)
                {
                    query = query.Where(s => s.ZoneId == _currentUser.ZoneId.Value);
                }
                break;

            case ManagementApi.Domain.Enums.OrganizationLevel.National:
                // National level - no automatic filtering
                break;
        }

        // Then apply additional filters from request (within user's scope)
        if (request.Request.ReportTemplateId.HasValue)
        {
            query = query.Where(s => s.ReportTemplateId == request.Request.ReportTemplateId.Value);
        }

        if (request.Request.Status.HasValue)
        {
            query = query.Where(s => s.Status == request.Request.Status.Value);
        }

        if (request.Request.MuqamId.HasValue)
        {
            query = query.Where(s => s.MuqamId == request.Request.MuqamId.Value);
        }

        if (request.Request.DilaId.HasValue)
        {
            query = query.Where(s => s.DilaId == request.Request.DilaId.Value);
        }

        if (request.Request.ZoneId.HasValue)
        {
            query = query.Where(s => s.ZoneId == request.Request.ZoneId.Value);
        }

        if (request.Request.OrganizationLevel.HasValue)
        {
            query = query.Where(s => s.OrganizationLevel == request.Request.OrganizationLevel.Value);
        }

        if (request.Request.StartDate.HasValue)
        {
            query = query.Where(s => s.SubmittedAt >= request.Request.StartDate.Value);
        }

        if (request.Request.EndDate.HasValue)
        {
            query = query.Where(s => s.SubmittedAt <= request.Request.EndDate.Value);
        }

        // Get total count
        var totalCount = await query.CountAsync(cancellationToken);

        // Apply pagination
        var submissions = await query
            .OrderByDescending(s => s.CreatedOn)
            .Skip((request.Request.PageNumber - 1) * request.Request.PageSize)
            .Take(request.Request.PageSize)
            .ToListAsync(cancellationToken);

        var submissionDtos = submissions.Select(s => new ReportSubmissionDto
        {
            Id = s.Id,
            ReportTemplateId = s.ReportTemplateId,
            ReportTemplateName = s.ReportTemplate.Name,
            SubmitterChandaNo = s.SubmitterChandaNo,
            SubmitterName = s.SubmitterName,
            SubmitterEmail = s.SubmitterEmail,
            OrganizationLevel = s.OrganizationLevel.ToString(),
            ResponseData = s.ResponseData,
            Status = s.Status.ToString(),
            SubmittedAt = s.SubmittedAt,
            RejectionReason = s.RejectionReason,
            CreatedOn = s.CreatedOn,
            Approvals = s.Approvals.Select(a => new SubmissionApprovalDto
            {
                Id = a.Id,
                ApproverName = a.ApproverName,
                Status = a.Status.ToString(),
                Comments = a.Comments,
                ActionDate = a.ActionDate
            }).ToList()
        }).ToList();

        var response = new PaginationResponse<ReportSubmissionDto>(
            submissionDtos,
            totalCount,
            request.Request.PageNumber,
            request.Request.PageSize);

        return Result<PaginationResponse<ReportSubmissionDto>>.Success(response);
    }
}
