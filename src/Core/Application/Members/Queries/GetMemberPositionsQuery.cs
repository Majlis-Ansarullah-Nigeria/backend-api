using FluentValidation;
using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Application.Members.DTOs;
using ManagementApi.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ManagementApi.Application.Members.Queries;

public record GetMemberPositionsQuery(Guid MemberId, bool? ActiveOnly = null) : IRequest<List<MemberPositionDto>>;

public class GetMemberPositionsQueryValidator : AbstractValidator<GetMemberPositionsQuery>
{
    public GetMemberPositionsQueryValidator()
    {
        RuleFor(x => x.MemberId)
            .NotEmpty().WithMessage("MemberId is required");
    }
}

public class GetMemberPositionsQueryHandler : IRequestHandler<GetMemberPositionsQuery, List<MemberPositionDto>>
{
    private readonly IApplicationDbContext _context;

    public GetMemberPositionsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<MemberPositionDto>> Handle(GetMemberPositionsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.MemberPositions
            .Where(mp => mp.MemberId == request.MemberId);

        if (request.ActiveOnly.HasValue && request.ActiveOnly.Value)
        {
            query = query.Where(mp => mp.IsActive);
        }

        // Materialize positions first
        var positions = await query
            .OrderByDescending(mp => mp.StartDate)
            .ToListAsync(cancellationToken);

        // Get all organization entity IDs grouped by level
        var zoneIds = positions.Where(p => p.OrganizationLevel == OrganizationLevel.Zone && p.OrganizationEntityId.HasValue)
            .Select(p => p.OrganizationEntityId!.Value).Distinct().ToList();
        var dilaIds = positions.Where(p => p.OrganizationLevel == OrganizationLevel.Dila && p.OrganizationEntityId.HasValue)
            .Select(p => p.OrganizationEntityId!.Value).Distinct().ToList();
        var muqamIds = positions.Where(p => p.OrganizationLevel == OrganizationLevel.Muqam && p.OrganizationEntityId.HasValue)
            .Select(p => p.OrganizationEntityId!.Value).Distinct().ToList();
        var jamaatIds = positions.Where(p => p.OrganizationLevel == OrganizationLevel.Jamaat && p.OrganizationEntityId.HasValue)
            .Select(p => p.OrganizationEntityId!.Value).Distinct().ToList();

        // Fetch organization names in batch
        var zoneNames = zoneIds.Any()
            ? await _context.Zones.Where(z => zoneIds.Contains(z.Id))
                .ToDictionaryAsync(z => z.Id, z => z.Name, cancellationToken)
            : new Dictionary<Guid, string>();

        var dilaNames = dilaIds.Any()
            ? await _context.Dilas.Where(d => dilaIds.Contains(d.Id))
                .ToDictionaryAsync(d => d.Id, d => d.Name, cancellationToken)
            : new Dictionary<Guid, string>();

        var muqamNames = muqamIds.Any()
            ? await _context.Muqams.Where(m => muqamIds.Contains(m.Id))
                .ToDictionaryAsync(m => m.Id, m => m.Name, cancellationToken)
            : new Dictionary<Guid, string>();

        var jamaatNames = jamaatIds.Any()
            ? await _context.Jamaats.Where(j => jamaatIds.Contains(j.Id))
                .ToDictionaryAsync(j => j.Id, j => j.Name, cancellationToken)
            : new Dictionary<Guid, string>();

        // Map to DTOs with organization names
        return positions.Select(p => new MemberPositionDto
        {
            Id = p.Id,
            MemberId = p.MemberId,
            PositionTitle = p.PositionTitle,
            OrganizationLevel = p.OrganizationLevel,
            OrganizationEntityId = p.OrganizationEntityId,
            OrganizationEntityName = GetOrganizationEntityName(p.OrganizationLevel, p.OrganizationEntityId,
                zoneNames, dilaNames, muqamNames, jamaatNames),
            StartDate = p.StartDate,
            EndDate = p.EndDate,
            IsActive = p.IsActive,
            Responsibilities = p.Responsibilities,
            CreatedOn = p.CreatedOn,
            CreatedBy = p.CreatedBy
        }).ToList();
    }

    private static string? GetOrganizationEntityName(
        OrganizationLevel level,
        Guid? entityId,
        Dictionary<Guid, string> zoneNames,
        Dictionary<Guid, string> dilaNames,
        Dictionary<Guid, string> muqamNames,
        Dictionary<Guid, string> jamaatNames)
    {
        if (!entityId.HasValue)
            return null;

        return level switch
        {
            OrganizationLevel.Zone => zoneNames.GetValueOrDefault(entityId.Value),
            OrganizationLevel.Dila => dilaNames.GetValueOrDefault(entityId.Value),
            OrganizationLevel.Muqam => muqamNames.GetValueOrDefault(entityId.Value),
            OrganizationLevel.Jamaat => jamaatNames.GetValueOrDefault(entityId.Value),
            _ => null
        };
    }
}
