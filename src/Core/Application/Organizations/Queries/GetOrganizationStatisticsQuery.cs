using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Application.Common.Models;
using ManagementApi.Application.Organizations.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ManagementApi.Application.Organizations.Queries;

public record GetOrganizationStatisticsQuery : IRequest<Result<OrganizationStatisticsDto>>;

public class GetOrganizationStatisticsQueryHandler : IRequestHandler<GetOrganizationStatisticsQuery, Result<OrganizationStatisticsDto>>
{
    private readonly IApplicationDbContext _context;

    public GetOrganizationStatisticsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<OrganizationStatisticsDto>> Handle(GetOrganizationStatisticsQuery request, CancellationToken cancellationToken)
    {
        var totalZones = await _context.Zones.CountAsync(cancellationToken);
        var totalDilas = await _context.Dilas.CountAsync(cancellationToken);
        var totalMuqams = await _context.Muqams.CountAsync(cancellationToken);
        var totalMembers = await _context.Members.CountAsync(cancellationToken);
        var totalJamaats = await _context.Jamaats.CountAsync(cancellationToken);
        var unassignedMuqams = await _context.Muqams.Where(m => m.DilaId == null).CountAsync(cancellationToken);
        var unassignedDilas = await _context.Dilas.Where(d => d.ZoneId == null).CountAsync(cancellationToken);

        var largestZone = await _context.Zones
            .Include(z => z.Dilas)
            .ThenInclude(d => d.Muqams)
            .ThenInclude(m => m.Members)
            .OrderByDescending(z => z.Dilas.SelectMany(d => d.Muqams).SelectMany(m => m.Members).Count())
            .Select(z => new ZoneStatsDto
            {
                Id = z.Id,
                Name = z.Name,
                DilaCount = z.Dilas.Count,
                MemberCount = z.Dilas.SelectMany(d => d.Muqams).SelectMany(m => m.Members).Count()
            })
            .FirstOrDefaultAsync(cancellationToken);

        var largestDila = await _context.Dilas
            .Include(d => d.Zone)
            .Include(d => d.Muqams)
            .ThenInclude(m => m.Members)
            .OrderByDescending(d => d.Muqams.SelectMany(m => m.Members).Count())
            .Select(d => new DilaStatsDto
            {
                Id = d.Id,
                Name = d.Name,
                ZoneName = d.Zone != null ? d.Zone.Name : null,
                MuqamCount = d.Muqams.Count,
                MemberCount = d.Muqams.SelectMany(m => m.Members).Count()
            })
            .FirstOrDefaultAsync(cancellationToken);

        var largestMuqam = await _context.Muqams
            .Include(m => m.Dila)
            .Include(m => m.Members)
            .Include(m => m.Jamaats)
            .OrderByDescending(m => m.Members.Count)
            .Select(m => new MuqamStatsDto
            {
                Id = m.Id,
                Name = m.Name,
                DilaName = m.Dila != null ? m.Dila.Name : null,
                MemberCount = m.Members.Count,
                JamaatCount = m.Jamaats.Count
            })
            .FirstOrDefaultAsync(cancellationToken);

        var statistics = new OrganizationStatisticsDto
        {
            TotalZones = totalZones,
            TotalDilas = totalDilas,
            TotalMuqams = totalMuqams,
            TotalMembers = totalMembers,
            TotalJamaats = totalJamaats,
            UnassignedMuqams = unassignedMuqams,
            UnassignedDilas = unassignedDilas,
            LargestZone = largestZone,
            LargestDila = largestDila,
            LargestMuqam = largestMuqam
        };

        return Result<OrganizationStatisticsDto>.Success(statistics);
    }
}
