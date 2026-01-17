using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ManagementApi.Application.Organizations.Commands;

public record DeleteDilaCommand(Guid Id) : IRequest<Result<bool>>;

public class DeleteDilaCommandHandler : IRequestHandler<DeleteDilaCommand, Result<bool>>
{
    private readonly IApplicationDbContext _context;

    public DeleteDilaCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<bool>> Handle(DeleteDilaCommand request, CancellationToken cancellationToken)
    {
        var dila = await _context.Dilas
            .Include(d => d.Muqams)
            .FirstOrDefaultAsync(d => d.Id == request.Id, cancellationToken);

        if (dila == null)
        {
            return Result<bool>.Failure("Dila not found");
        }

        // Check if dila has muqams
        if (dila.Muqams.Any())
        {
            return Result<bool>.Failure($"Cannot delete Dila. It has {dila.Muqams.Count} muqams. Please reassign or delete muqams first.");
        }

        _context.Dilas.Remove(dila);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<bool>.Success(true, "Dila deleted successfully");
    }
}
