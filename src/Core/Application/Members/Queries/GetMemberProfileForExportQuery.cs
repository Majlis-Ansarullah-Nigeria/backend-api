using FluentValidation;
using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Application.Members.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ManagementApi.Application.Members.Queries;

public record GetMemberProfileForExportQuery(Guid MemberId) : IRequest<MemberProfileExportDto?>;

public class GetMemberProfileForExportQueryValidator : AbstractValidator<GetMemberProfileForExportQuery>
{
    public GetMemberProfileForExportQueryValidator()
    {
        RuleFor(x => x.MemberId)
            .NotEmpty().WithMessage("MemberId is required");
    }
}

public class GetMemberProfileForExportQueryHandler : IRequestHandler<GetMemberProfileForExportQuery, MemberProfileExportDto?>
{
    private readonly IApplicationDbContext _context;

    public GetMemberProfileForExportQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<MemberProfileExportDto?> Handle(GetMemberProfileForExportQuery request, CancellationToken cancellationToken)
    {
        var member = await _context.Members
            .FirstOrDefaultAsync(m => m.Id == request.MemberId, cancellationToken);

        if (member == null)
            return null;

        // Get Jamaat with full hierarchy
        var jamaat = member.JamaatId.HasValue
            ? await _context.Jamaats
                .Include(j => j.Muqam)
                    .ThenInclude(m => m!.Dila)
                        .ThenInclude(d => d!.Zone)
                .FirstOrDefaultAsync(j => j.JamaatId == member.JamaatId.Value, cancellationToken)
            : null;

        // Get positions
        var positions = await _context.MemberPositions
            .Where(mp => mp.MemberId == request.MemberId)
            .OrderByDescending(mp => mp.StartDate)
            .ToListAsync(cancellationToken);

        // Get organization names for positions
        var zoneIds = positions.Where(p => p.OrganizationLevel == Domain.Enums.OrganizationLevel.Zone && p.OrganizationEntityId.HasValue)
            .Select(p => p.OrganizationEntityId!.Value).Distinct().ToList();
        var dilaIds = positions.Where(p => p.OrganizationLevel == Domain.Enums.OrganizationLevel.Dila && p.OrganizationEntityId.HasValue)
            .Select(p => p.OrganizationEntityId!.Value).Distinct().ToList();
        var muqamIds = positions.Where(p => p.OrganizationLevel == Domain.Enums.OrganizationLevel.Muqam && p.OrganizationEntityId.HasValue)
            .Select(p => p.OrganizationEntityId!.Value).Distinct().ToList();
        var jamaatIds = positions.Where(p => p.OrganizationLevel == Domain.Enums.OrganizationLevel.Jamaat && p.OrganizationEntityId.HasValue)
            .Select(p => p.OrganizationEntityId!.Value).Distinct().ToList();

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

        var positionDtos = positions.Select(p => new MemberPositionDto
        {
            Id = p.Id,
            MemberId = p.MemberId,
            PositionTitle = p.PositionTitle,
            OrganizationLevel = p.OrganizationLevel,
            OrganizationEntityId = p.OrganizationEntityId,
            OrganizationEntityName = GetOrganizationName(p.OrganizationLevel, p.OrganizationEntityId,
                zoneNames, dilaNames, muqamNames, jamaatNames),
            StartDate = p.StartDate,
            EndDate = p.EndDate,
            IsActive = p.IsActive,
            Responsibilities = p.Responsibilities,
            CreatedOn = p.CreatedOn,
            CreatedBy = p.CreatedBy
        }).ToList();

        return new MemberProfileExportDto
        {
            Id = member.Id,
            ChandaNo = member.ChandaNo,
            WasiyatNo = member.WasiyatNo,
            Title = member.Title,
            Surname = member.Surname,
            FirstName = member.FirstName,
            MiddleName = member.MiddleName,
            DateOfBirth = member.DateOfBirth,
            Email = member.Email,
            PhoneNo = member.PhoneNo,
            MaritalStatus = member.MaritalStatus,
            Address = member.Address,
            NextOfKinPhoneNo = member.NextOfKinPhoneNo,
            NextOfKinName = member.NextOfKinName,
            RecordStatus = member.RecordStatus,
            MemberShipStatus = member.MemberShipStatus,
            PhotoUrl = member.PhotoUrl,
            Signature = member.Signature,
            BloodGroup = member.BloodGroup,
            Genotype = member.Genotype,
            JamaatName = jamaat?.Name,
            MuqamName = jamaat?.Muqam?.Name,
            DilaName = jamaat?.Muqam?.Dila?.Name,
            ZoneName = jamaat?.Muqam?.Dila?.Zone?.Name,
            Positions = positionDtos,
            ExportedAt = DateTime.UtcNow
        };
    }

    private static string? GetOrganizationName(
        Domain.Enums.OrganizationLevel level,
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
            Domain.Enums.OrganizationLevel.Zone => zoneNames.GetValueOrDefault(entityId.Value),
            Domain.Enums.OrganizationLevel.Dila => dilaNames.GetValueOrDefault(entityId.Value),
            Domain.Enums.OrganizationLevel.Muqam => muqamNames.GetValueOrDefault(entityId.Value),
            Domain.Enums.OrganizationLevel.Jamaat => jamaatNames.GetValueOrDefault(entityId.Value),
            _ => null
        };
    }
}
