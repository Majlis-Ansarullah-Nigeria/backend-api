using FluentValidation;
using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Application.Common.Models;
using ManagementApi.Application.Reports.Common;
using ManagementApi.Application.Reports.DTOs;
using MediatR;

namespace ManagementApi.Application.Reports.Commands;

public record SubmitReportCommand(SubmitReportRequest Request) : IRequest<Result<Guid>>;

public class SubmitReportCommandValidator : AbstractValidator<SubmitReportCommand>
{
    public SubmitReportCommandValidator()
    {
        RuleFor(x => x.Request.ReportTemplateId)
            .NotEmpty().WithMessage("Report template ID is required");

        RuleFor(x => x.Request.SubmissionWindowId)
            .NotEmpty().WithMessage("Submission window is required");

        RuleFor(x => x.Request.ResponseData)
            .NotEmpty().WithMessage("Response data is required");
    }
}

/// <summary>
/// Submits (or resubmits after a return) the current user's own unit report for a window.
/// </summary>
public class SubmitReportCommandHandler : IRequestHandler<SubmitReportCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly TimeProvider _clock;

    public SubmitReportCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService, TimeProvider clock)
    {
        _context = context;
        _currentUserService = currentUserService;
        _clock = clock;
    }

    public async Task<Result<Guid>> Handle(SubmitReportCommand request, CancellationToken cancellationToken)
    {
        var now = _clock.GetUtcNow().UtcDateTime;

        var (loaded, error) = await OwnReport.LoadOrCreateAsync(
            _context,
            _currentUserService,
            request.Request.ReportTemplateId,
            request.Request.SubmissionWindowId!.Value,
            request.Request.ResponseData,
            now,
            cancellationToken);

        if (loaded is null)
        {
            return Result<Guid>.Failure(error!);
        }

        try
        {
            if (!loaded.IsNew)
            {
                loaded.Report.SaveDraft(
                    request.Request.ResponseData,
                    _currentUserService.UserId,
                    _currentUserService.ChandaNo ?? "UNKNOWN",
                    _currentUserService.UserName ?? "Unknown User",
                    _currentUserService.Email);
            }

            loaded.Report.Submit(now, loaded.IsLate);
        }
        catch (InvalidOperationException ex)
        {
            return Result<Guid>.Failure(ex.Message);
        }

        await _context.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(loaded.Report.Id, loaded.IsLate
            ? "Report submitted after the due date and marked late"
            : "Report submitted successfully");
    }
}
