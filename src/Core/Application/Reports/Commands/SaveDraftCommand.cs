using FluentValidation;
using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Application.Common.Models;
using ManagementApi.Application.Reports.Common;
using ManagementApi.Application.Reports.DTOs;
using MediatR;

namespace ManagementApi.Application.Reports.Commands;

public record SaveDraftCommand(SaveDraftRequest Request) : IRequest<Result<Guid>>;

public class SaveDraftCommandValidator : AbstractValidator<SaveDraftCommand>
{
    public SaveDraftCommandValidator()
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
/// Saves the current user's own unit report for a window as a draft. Draft and Returned reports are editable.
/// </summary>
public class SaveDraftCommandHandler : IRequestHandler<SaveDraftCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly TimeProvider _clock;

    public SaveDraftCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService, TimeProvider clock)
    {
        _context = context;
        _currentUserService = currentUserService;
        _clock = clock;
    }

    public async Task<Result<Guid>> Handle(SaveDraftCommand request, CancellationToken cancellationToken)
    {
        var (loaded, error) = await OwnReport.LoadOrCreateAsync(
            _context,
            _currentUserService,
            request.Request.ReportTemplateId,
            request.Request.SubmissionWindowId!.Value,
            request.Request.ResponseData,
            _clock.GetUtcNow().UtcDateTime,
            cancellationToken);

        if (loaded is null)
        {
            return Result<Guid>.Failure(error!);
        }

        if (!loaded.IsNew)
        {
            try
            {
                loaded.Report.SaveDraft(
                    request.Request.ResponseData,
                    _currentUserService.UserId,
                    _currentUserService.ChandaNo ?? "UNKNOWN",
                    _currentUserService.UserName ?? "Unknown User",
                    _currentUserService.Email);
            }
            catch (InvalidOperationException ex)
            {
                return Result<Guid>.Failure(ex.Message);
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(loaded.Report.Id, loaded.IsNew ? "Draft saved successfully" : "Draft updated successfully");
    }
}
