using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Application.Common.Models;
using ManagementApi.Application.Reports.DTOs;
using ManagementApi.Domain.Entities.Reports;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ManagementApi.Application.Reports.Queries;

public record GetSubmissionByIdQuery(Guid SubmissionId) : IRequest<Result<ReportSubmissionDto>>;

public class GetSubmissionByIdQueryHandler : IRequestHandler<GetSubmissionByIdQuery, Result<ReportSubmissionDto>>
{
    private readonly IApplicationDbContext _context;

    public GetSubmissionByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<ReportSubmissionDto>> Handle(GetSubmissionByIdQuery request, CancellationToken cancellationToken)
    {
        var submission = await _context.ReportSubmissions
            .Include(s => s.ReportTemplate)
            .Include(s => s.Approvals)
            .FirstOrDefaultAsync(s => s.Id == request.SubmissionId, cancellationToken);

        if (submission == null)
        {
            return Result<ReportSubmissionDto>.Failure("Submission not found");
        }

        var submissionDto = new ReportSubmissionDto
        {
            Id = submission.Id,
            ReportTemplateId = submission.ReportTemplateId,
            ReportTemplateName = submission.ReportTemplate.Name,
            SubmitterChandaNo = submission.SubmitterChandaNo,
            SubmitterName = submission.SubmitterName,
            SubmitterEmail = submission.SubmitterEmail,
            OrganizationLevel = submission.OrganizationLevel.ToString(),
            MuqamName = submission.MuqamId.HasValue ? await GetOrganizationName(submission.MuqamId.Value, "Muqam") : null,
            DilaName = submission.DilaId.HasValue ? await GetOrganizationName(submission.DilaId.Value, "Dila") : null,
            ZoneName = submission.ZoneId.HasValue ? await GetOrganizationName(submission.ZoneId.Value, "Zone") : null,
            ResponseData = submission.ResponseData,
            Status = submission.Status.ToString(),
            SubmittedAt = submission.SubmittedAt,
            RejectionReason = submission.RejectionReason,
            CreatedOn = submission.CreatedOn,
            Approvals = submission.Approvals.Select(a => new SubmissionApprovalDto
            {
                Id = a.Id,
                ApproverName = a.ApproverName,
                Status = a.Status.ToString(),
                Comments = a.Comments,
                ActionDate = a.ActionDate
            }).ToList()
        };

        return Result<ReportSubmissionDto>.Success(submissionDto);
    }

    private async Task<string?> GetOrganizationName(Guid id, string entityType)
    {
        switch (entityType.ToLower())
        {
            case "muqam":
                return await _context.Muqams.Where(m => m.Id == id).Select(m => m.Name).FirstOrDefaultAsync();
            case "dila":
                return await _context.Dilas.Where(d => d.Id == id).Select(d => d.Name).FirstOrDefaultAsync();
            case "zone":
                return await _context.Zones.Where(z => z.Id == id).Select(z => z.Name).FirstOrDefaultAsync();
            default:
                return null;
        }
    }
}