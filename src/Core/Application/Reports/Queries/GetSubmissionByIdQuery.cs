using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Application.Common.Models;
using ManagementApi.Application.Reports.Common;
using ManagementApi.Application.Reports.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ManagementApi.Application.Reports.Queries;

public record GetSubmissionByIdQuery(Guid SubmissionId) : IRequest<Result<ReportSubmissionDto>>;

public class GetSubmissionByIdQueryHandler : IRequestHandler<GetSubmissionByIdQuery, Result<ReportSubmissionDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetSubmissionByIdQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<ReportSubmissionDto>> Handle(GetSubmissionByIdQuery request, CancellationToken cancellationToken)
    {
        // Reports outside the user's scope are reported as not found, so ids cannot be probed.
        var submission = await _context.ReportSubmissions
            .Include(s => s.ReportTemplate)
            .Include(s => s.Approvals)
            .Include(s => s.SubmissionWindow!.Extensions)
            .AsNoTracking()
            .VisibleTo(_currentUser)
            .FirstOrDefaultAsync(s => s.Id == request.SubmissionId, cancellationToken);

        if (submission == null)
        {
            return Result<ReportSubmissionDto>.Failure("Submission not found");
        }

        var names = await UnitNames.LoadAsync(_context, new[] { submission }, cancellationToken);

        var flagged = await _context.SubmissionFlags.AnyAsync(f => f.IsActive && f.ReportSubmissionId == submission.Id, cancellationToken);

        return Result<ReportSubmissionDto>.Success(
            names.Apply(ReportSubmissionDto.From(submission, _currentUser), submission) with { IsFlagged = flagged });
    }
}
