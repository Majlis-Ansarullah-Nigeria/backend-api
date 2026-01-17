using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ManagementApi.Application.Organizations.Commands;

public record DeleteMuqamCommand(Guid Id) : IRequest<Result<bool>>;

public class DeleteMuqamCommandHandler : IRequestHandler<DeleteMuqamCommand, Result<bool>>
{
    private readonly IApplicationDbContext _context;

    public DeleteMuqamCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<bool>> Handle(DeleteMuqamCommand request, CancellationToken cancellationToken)
    {
        var muqam = await _context.Muqams
            .Include(m => m.Members)
            .Include(m => m.Jamaats)
            .FirstOrDefaultAsync(m => m.Id == request.Id, cancellationToken);

        if (muqam == null)
        {
            return Result<bool>.Failure("Muqam not found");
        }

        // Check if muqam has members or jamaats
        if (muqam.Members.Any())
        {
            return Result<bool>.Failure($"Cannot delete Muqam. It has {muqam.Members.Count} members. Please reassign members first.");
        }

        if (muqam.Jamaats.Any())
        {
            return Result<bool>.Failure($"Cannot delete Muqam. It has {muqam.Jamaats.Count} jamaats. Please reassign jamaats first.");
        }

        _context.Muqams.Remove(muqam);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<bool>.Success(true, "Muqam deleted successfully");
    }
}
