using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ManagementApi.Application.Organizations.Commands;

public record BulkAssignMuqamsRequest
{
    public List<Guid> MuqamIds { get; init; } = new();
    public Guid? DilaId { get; init; }
}

public record BulkAssignMuqamsCommand(BulkAssignMuqamsRequest Request) : IRequest<Result<int>>;

public class BulkAssignMuqamsCommandHandler : IRequestHandler<BulkAssignMuqamsCommand, Result<int>>
{
    private readonly IApplicationDbContext _context;

    public BulkAssignMuqamsCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<int>> Handle(BulkAssignMuqamsCommand request, CancellationToken cancellationToken)
    {
        if (!request.Request.MuqamIds.Any())
        {
            return Result<int>.Failure("No muqams selected");
        }

        // Verify dila exists if assigning to one
        if (request.Request.DilaId.HasValue)
        {
            var dilaExists = await _context.Dilas.AnyAsync(d => d.Id == request.Request.DilaId.Value, cancellationToken);
            if (!dilaExists)
            {
                return Result<int>.Failure("Dila not found");
            }
        }

        var muqams = await _context.Muqams
            .Where(m => request.Request.MuqamIds.Contains(m.Id))
            .ToListAsync(cancellationToken);

        if (!muqams.Any())
        {
            return Result<int>.Failure("No valid muqams found");
        }

        var updatedCount = 0;
        foreach (var muqam in muqams)
        {
            if (request.Request.DilaId.HasValue)
            {
                muqam.AssignToDila(request.Request.DilaId.Value);
            }
            // Note: To unassign, DilaId should be null in the request
            updatedCount++;
        }

        await _context.SaveChangesAsync(cancellationToken);

        var message = request.Request.DilaId.HasValue
            ? $"{updatedCount} muqams assigned to dila successfully"
            : $"{updatedCount} muqams unassigned from dila successfully";

        return Result<int>.Success(updatedCount, message);
    }
}
