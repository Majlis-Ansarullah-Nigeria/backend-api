using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Application.Common.Models;
using ManagementApi.Application.Jamaats.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ManagementApi.Application.Jamaats.Queries;

public record GetJamaatMappingStatsQuery : IRequest<Result<JamaatMappingStatsDto>>;

public class GetJamaatMappingStatsQueryHandler : IRequestHandler<GetJamaatMappingStatsQuery, Result<JamaatMappingStatsDto>>
{
    private readonly IApplicationDbContext _context;

    public GetJamaatMappingStatsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<JamaatMappingStatsDto>> Handle(GetJamaatMappingStatsQuery request, CancellationToken cancellationToken)
    {
        var totalJamaats = await _context.Jamaats.CountAsync(cancellationToken);
        var mappedJamaats = await _context.Jamaats.CountAsync(j => j.MuqamId != null, cancellationToken);
        var unmappedJamaats = totalJamaats - mappedJamaats;

        var mappingPercentage = totalJamaats > 0 ? (double)mappedJamaats / totalJamaats * 100 : 0;

        var stats = new JamaatMappingStatsDto
        {
            TotalJamaats = totalJamaats,
            MappedJamaats = mappedJamaats,
            UnmappedJamaats = unmappedJamaats,
            MappingPercentage = Math.Round(mappingPercentage, 2)
        };

        return Result<JamaatMappingStatsDto>.Success(stats);
    }
}
