using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ManagementApi.Application.Organizations.Commands;

public record DeleteZoneCommand(Guid Id) : IRequest<Result<bool>>;

public class DeleteZoneCommandHandler : IRequestHandler<DeleteZoneCommand, Result<bool>>
{
    private readonly IApplicationDbContext _context;

    public DeleteZoneCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<bool>> Handle(DeleteZoneCommand request, CancellationToken cancellationToken)
    {
        var zone = await _context.Zones
            .Include(z => z.Dilas)
            .FirstOrDefaultAsync(z => z.Id == request.Id, cancellationToken);

        if (zone == null)
        {
            return Result<bool>.Failure("Zone not found");
        }

        // Check if zone has dilas
        if (zone.Dilas.Any())
        {
            return Result<bool>.Failure($"Cannot delete Zone. It has {zone.Dilas.Count} dilas. Please reassign or delete dilas first.");
        }

        _context.Zones.Remove(zone);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<bool>.Success(true, "Zone deleted successfully");
    }
}
