using FluentValidation;
using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Application.Common.Models;
using ManagementApi.Application.Reports.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ManagementApi.Application.Reports.Commands;

public record UpdateCommentCommand(UpdateCommentRequest Request) : IRequest<Result>;

public class UpdateCommentCommandValidator : AbstractValidator<UpdateCommentCommand>
{
    public UpdateCommentCommandValidator()
    {
        RuleFor(x => x.Request.CommentId)
            .NotEmpty().WithMessage("Comment ID is required");

        RuleFor(x => x.Request.Content)
            .NotEmpty().WithMessage("Comment content is required")
            .MaximumLength(2000).WithMessage("Comment content cannot exceed 2000 characters");
    }
}

public class UpdateCommentCommandHandler : IRequestHandler<UpdateCommentCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public UpdateCommentCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result> Handle(UpdateCommentCommand request, CancellationToken cancellationToken)
    {
        var comment = await _context.SubmissionComments
            .FirstOrDefaultAsync(c => c.Id == request.Request.CommentId, cancellationToken);

        if (comment == null)
        {
            return Result.Failure("Comment not found");
        }

        try
        {
            var currentUserId = _currentUserService.UserId;
            comment.UpdateContent(request.Request.Content, currentUserId);

            await _context.SaveChangesAsync(cancellationToken);

            return Result.Success("Comment updated successfully");
        }
        catch (UnauthorizedAccessException ex)
        {
            return Result.Failure(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return Result.Failure(ex.Message);
        }
    }
}
