using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Application.Members.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ManagementApi.Application.Members.Queries;

public record GetMemberStatisticsQuery : IRequest<MemberStatisticsDto>;

public class GetMemberStatisticsQueryHandler : IRequestHandler<GetMemberStatisticsQuery, MemberStatisticsDto>
{
    private readonly IApplicationDbContext _context;

    public GetMemberStatisticsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<MemberStatisticsDto> Handle(GetMemberStatisticsQuery request, CancellationToken cancellationToken)
    {
        var totalMembers = await _context.Members.CountAsync(cancellationToken);
        var activeMembers = await _context.Members.CountAsync(m => m.RecordStatus == true, cancellationToken);
        var inactiveMembers = totalMembers - activeMembers;

        // Members by blood group
        var bloodGroupStats = await _context.Members
            .Where(m => m.BloodGroup != null)
            .GroupBy(m => m.BloodGroup)
            .Select(g => new BloodGroupStatDto
            {
                BloodGroup = g.Key!.ToString(),
                Count = g.Count()
            })
            .ToListAsync(cancellationToken);

        // Members by genotype
        var genotypeStats = await _context.Members
            .Where(m => m.Genotype != null)
            .GroupBy(m => m.Genotype)
            .Select(g => new GenotypeStatDto
            {
                Genotype = g.Key!.ToString(),
                Count = g.Count()
            })
            .ToListAsync(cancellationToken);

        // Members by organization
        var membersByZone = await _context.Jamaats
            .Include(j => j.Muqam)
                .ThenInclude(m => m!.Dila)
                    .ThenInclude(d => d!.Zone)
            .Join(_context.Members,
                j => j.JamaatId,
                m => m.JamaatId,
                (j, m) => new { Jamaat = j, Member = m })
            .Where(x => x.Jamaat.Muqam != null && x.Jamaat.Muqam.Dila != null && x.Jamaat.Muqam.Dila.Zone != null)
            .GroupBy(x => new { x.Jamaat.Muqam!.Dila!.Zone!.Id, x.Jamaat.Muqam!.Dila!.Zone!.Name })
            .Select(g => new OrganizationStatDto
            {
                OrganizationId = g.Key.Id.ToString(),
                OrganizationName = g.Key.Name,
                MemberCount = g.Count()
            })
            .ToListAsync(cancellationToken);

        // Members by marital status
        var maritalStatusStats = await _context.Members
            .Where(m => !string.IsNullOrEmpty(m.MaritalStatus))
            .GroupBy(m => m.MaritalStatus)
            .Select(g => new MaritalStatusStatDto
            {
                MaritalStatus = g.Key!,
                Count = g.Count()
            })
            .ToListAsync(cancellationToken);

        // Age distribution
        var currentYear = DateTime.UtcNow.Year;
        var membersWithAge = await _context.Members
            .Where(m => m.DateOfBirth != null)
            .Select(m => currentYear - m.DateOfBirth!.Value.Year)
            .ToListAsync(cancellationToken);

        var ageDistribution = new List<AgeDistributionDto>
        {
            new() { AgeRange = "Under 18", Count = membersWithAge.Count(a => a < 18) },
            new() { AgeRange = "18-30", Count = membersWithAge.Count(a => a >= 18 && a <= 30) },
            new() { AgeRange = "31-45", Count = membersWithAge.Count(a => a >= 31 && a <= 45) },
            new() { AgeRange = "46-60", Count = membersWithAge.Count(a => a >= 46 && a <= 60) },
            new() { AgeRange = "Over 60", Count = membersWithAge.Count(a => a > 60) }
        };

        // Members with positions
        var membersWithPositions = await _context.MemberPositions
            .Where(mp => mp.IsActive)
            .Select(mp => mp.MemberId)
            .Distinct()
            .CountAsync(cancellationToken);

        return new MemberStatisticsDto
        {
            TotalMembers = totalMembers,
            ActiveMembers = activeMembers,
            InactiveMembers = inactiveMembers,
            MembersWithPositions = membersWithPositions,
            BloodGroupDistribution = bloodGroupStats,
            GenotypeDistribution = genotypeStats,
            MembersByZone = membersByZone,
            MaritalStatusDistribution = maritalStatusStats,
            AgeDistribution = ageDistribution,
            GeneratedAt = DateTime.UtcNow
        };
    }
}
