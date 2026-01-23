using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Application.Members.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ManagementApi.Application.Members.Queries;

public record GetMemberStatisticsQuery : IRequest<MemberStatisticsDto>;

public class GetMemberStatisticsQueryHandler : IRequestHandler<GetMemberStatisticsQuery, MemberStatisticsDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetMemberStatisticsQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<MemberStatisticsDto> Handle(GetMemberStatisticsQuery request, CancellationToken cancellationToken)
    {
        // SECURITY: Apply automatic filtering based on current user's organization level
        // This ensures users only see statistics for their organizational scope
        var membersQuery = _context.Members.AsQueryable();
        var jamaatIds = await GetAuthorizedJamaatIdsAsync(cancellationToken);

        if (jamaatIds != null && jamaatIds.Any())
        {
            membersQuery = membersQuery.Where(m => m.JamaatId.HasValue && jamaatIds.Contains(m.JamaatId.Value));
        }

        var totalMembers = await membersQuery.CountAsync(cancellationToken);
        var activeMembers = await membersQuery.CountAsync(m => m.RecordStatus == true, cancellationToken);
        var inactiveMembers = totalMembers - activeMembers;

        // Members by blood group (filtered)
        var bloodGroupStats = await membersQuery
            .Where(m => m.BloodGroup != null)
            .GroupBy(m => m.BloodGroup)
            .Select(g => new BloodGroupStatDto
            {
                BloodGroup = g.Key!.ToString(),
                Count = g.Count()
            })
            .ToListAsync(cancellationToken);

        // Members by genotype (filtered)
        var genotypeStats = await membersQuery
            .Where(m => m.Genotype != null)
            .GroupBy(m => m.Genotype)
            .Select(g => new GenotypeStatDto
            {
                Genotype = g.Key!.ToString(),
                Count = g.Count()
            })
            .ToListAsync(cancellationToken);

        // Members by organization (filtered by authorized jamaats)
        var membersByZoneQuery = _context.Jamaats
            .Include(j => j.Muqam)
                .ThenInclude(m => m!.Dila)
                    .ThenInclude(d => d!.Zone)
            .Join(membersQuery,
                j => j.JamaatId,
                m => m.JamaatId,
                (j, m) => new { Jamaat = j, Member = m })
            .Where(x => x.Jamaat.Muqam != null && x.Jamaat.Muqam.Dila != null && x.Jamaat.Muqam.Dila.Zone != null);

        var membersByZone = await membersByZoneQuery
            .GroupBy(x => new { x.Jamaat.Muqam!.Dila!.Zone!.Id, x.Jamaat.Muqam!.Dila!.Zone!.Name })
            .Select(g => new OrganizationStatDto
            {
                OrganizationId = g.Key.Id.ToString(),
                OrganizationName = g.Key.Name,
                MemberCount = g.Count()
            })
            .ToListAsync(cancellationToken);

        // Members by marital status (filtered)
        var maritalStatusStats = await membersQuery
            .Where(m => !string.IsNullOrEmpty(m.MaritalStatus))
            .GroupBy(m => m.MaritalStatus)
            .Select(g => new MaritalStatusStatDto
            {
                MaritalStatus = g.Key!,
                Count = g.Count()
            })
            .ToListAsync(cancellationToken);

        // Age distribution (filtered)
        var currentYear = DateTime.UtcNow.Year;
        var membersWithAge = await membersQuery
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

        // Members with positions (filtered)
        var memberIds = await membersQuery.Select(m => m.Id).ToListAsync(cancellationToken);
        var membersWithPositions = await _context.MemberPositions
            .Where(mp => mp.IsActive && memberIds.Contains(mp.MemberId))
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

    /// <summary>
    /// Get authorized Jamaat IDs based on user's organization level
    /// Returns null for National level (no filtering), otherwise returns list of allowed Jamaat IDs
    /// </summary>
    private async Task<List<int>?> GetAuthorizedJamaatIdsAsync(CancellationToken cancellationToken)
    {
        var orgLevel = _currentUser.OrganizationLevel;

        switch (orgLevel)
        {
            case ManagementApi.Domain.Enums.OrganizationLevel.Muqam:
                // ZaimAla sees only their Muqam's Jamaats
                if (_currentUser.MuqamId.HasValue)
                {
                    return await _context.Jamaats
                        .Where(j => j.MuqamId == _currentUser.MuqamId.Value)
                        .Select(j => j.JamaatId)
                        .ToListAsync(cancellationToken);
                }
                break;

            case ManagementApi.Domain.Enums.OrganizationLevel.Dila:
                // NazimAla sees all Muqams under their Dila
                if (_currentUser.DilaId.HasValue)
                {
                    return await _context.Jamaats
                        .Where(j => j.Muqam != null && j.Muqam.DilaId == _currentUser.DilaId.Value)
                        .Select(j => j.JamaatId)
                        .ToListAsync(cancellationToken);
                }
                break;

            case ManagementApi.Domain.Enums.OrganizationLevel.Zone:
                // ZoneNazim sees all Dilas and Muqams under their Zone
                if (_currentUser.ZoneId.HasValue)
                {
                    return await _context.Jamaats
                        .Where(j => j.Muqam != null &&
                                   j.Muqam.Dila != null &&
                                   j.Muqam.Dila.ZoneId == _currentUser.ZoneId.Value)
                        .Select(j => j.JamaatId)
                        .ToListAsync(cancellationToken);
                }
                break;

            case ManagementApi.Domain.Enums.OrganizationLevel.National:
                // National level - no filtering
                return null;
        }

        // Default: empty list (no access)
        return new List<int>();
    }
}
