using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Application.Common.Models;
using ManagementApi.Application.Jamaats.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ManagementApi.Application.Jamaats.Queries;

public record GetJamaatsQuery(bool? OnlyUnmapped = null) : IRequest<Result<List<JamaatDto>>>;

public class GetJamaatsQueryHandler : IRequestHandler<GetJamaatsQuery, Result<List<JamaatDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetJamaatsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<JamaatDto>>> Handle(GetJamaatsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Jamaats
            .Include(j => j.Muqam)
            .AsQueryable();

        // Filter by mapping status if specified
        if (request.OnlyUnmapped.HasValue && request.OnlyUnmapped.Value)
        {
            query = query.Where(j => j.MuqamId == null);
        }

        var jamaats = await query
            .OrderBy(j => j.Name)
            .Select(j => new JamaatDto
            {
                Id = j.Id,
                JamaatId = j.JamaatId,
                Name = j.Name,
                Code = j.Code,
                CircuitName = j.CircuitName,
                MuqamId = j.MuqamId,
                MuqamName = j.Muqam != null ? j.Muqam.Name : null,
                CreatedOn = j.CreatedOn
            })
            .ToListAsync(cancellationToken);

        return Result<List<JamaatDto>>.Success(jamaats);
    }
}
