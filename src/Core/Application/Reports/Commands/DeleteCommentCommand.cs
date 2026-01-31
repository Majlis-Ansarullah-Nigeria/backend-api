using FluentValidation;
using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Application.Common.Models;
using ManagementApi.Application.Reports.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ManagementApi.Application.Reports.Commands;

public record DeleteCommentCommand(DeleteCommentRequest Request) : IRequest<Result>;

public class DeleteCommentCommandValidator : AbstractValidator<DeleteCommentCommand>
{
    public DeleteCommentCommandValidator()
    {
        RuleFor(x => x.Request.CommentId)
            .NotEmpty().WithMessage("Comment ID is required");
    }
}

public class DeleteCommentCommandHandler : IRequestHandler<DeleteCommentCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public DeleteCommentCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result> Handle(DeleteCommentCommand request, CancellationToken cancellationToken)
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
            comment.Delete(currentUserId);

            await _context.SaveChangesAsync(cancellationToken);

            return Result.Success("Comment deleted successfully");
        }
        catch (UnauthorizedAccessException ex)
        {
            return Result.Failure(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure(ex.Message);
        }
    }
}
