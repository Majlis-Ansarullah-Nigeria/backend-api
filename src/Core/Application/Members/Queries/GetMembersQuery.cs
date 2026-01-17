using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Application.Common.Models;
using ManagementApi.Application.Members.DTOs;
using ManagementApi.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ManagementApi.Application.Members.Queries;

public record GetMembersQuery(int PageNumber = 1, int PageSize = 10) : IRequest<PaginationResponse<MemberDto>>;

public class GetMembersQueryHandler : IRequestHandler<GetMembersQuery, PaginationResponse<MemberDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetMembersQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<PaginationResponse<MemberDto>> Handle(GetMembersQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Members
            .Include(m => m.Muqam)
                .ThenInclude(m => m!.Dila)
                    .ThenInclude(d => d!.Zone)
            .AsQueryable();

        // Apply automatic filtering based on current user's organization level
        var orgLevel = _currentUser.OrganizationLevel;

        switch (orgLevel)
        {
            case OrganizationLevel.Muqam:
                if (_currentUser.MuqamId.HasValue)
                {
                    // Get members through Jamaat relationship
                    var muqamJamaatIds = _context.Jamaats
                        .Where(j => j.MuqamId == _currentUser.MuqamId.Value)
                        .Select(j => j.JamaatId)
                        .ToList();

                    query = query.Where(m => m.JamaatId.HasValue && muqamJamaatIds.Contains(m.JamaatId.Value));
                }
                break;

            case OrganizationLevel.Dila:
                if (_currentUser.DilaId.HasValue)
                {
                    // Get members through Jamaat -> Muqam -> Dila relationship
                    var dilaJamaatIds = _context.Jamaats
                        .Where(j => j.Muqam != null && j.Muqam.DilaId == _currentUser.DilaId.Value)
                        .Select(j => j.JamaatId)
                        .ToList();

                    query = query.Where(m => m.JamaatId.HasValue && dilaJamaatIds.Contains(m.JamaatId.Value));
                }
                break;

            case OrganizationLevel.Zone:
                if (_currentUser.ZoneId.HasValue)
                {
                    // Get members through Jamaat -> Muqam -> Dila -> Zone relationship
                    var zoneJamaatIds = _context.Jamaats
                        .Where(j => j.Muqam != null &&
                                   j.Muqam.Dila != null &&
                                   j.Muqam.Dila.ZoneId == _currentUser.ZoneId.Value)
                        .Select(j => j.JamaatId)
                        .ToList();

                    query = query.Where(m => m.JamaatId.HasValue && zoneJamaatIds.Contains(m.JamaatId.Value));
                }
                break;

            case OrganizationLevel.National:
                // National level - no filtering, see all members
                break;
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var members = await query
            .OrderBy(m => m.Surname)
            .ThenBy(m => m.FirstName)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(m => new
            {
                Member = m,
                Jamaat = m.JamaatId.HasValue
                    ? _context.Jamaats
                        .Include(j => j.Muqam)
                            .ThenInclude(mu => mu!.Dila)
                                .ThenInclude(d => d!.Zone)
                        .FirstOrDefault(j => j.JamaatId == m.JamaatId.Value)
                    : null
            })
            .ToListAsync(cancellationToken);

        var memberDtos = members.Select(x => new MemberDto
        {
            Id = x.Member.Id,
            ChandaNo = x.Member.ChandaNo,
            WasiyatNo = x.Member.WasiyatNo,
            Title = x.Member.Title,
            Surname = x.Member.Surname,
            FirstName = x.Member.FirstName,
            MiddleName = x.Member.MiddleName,
            DateOfBirth = x.Member.DateOfBirth,
            Email = x.Member.Email,
            PhoneNo = x.Member.PhoneNo,
            MaritalStatus = x.Member.MaritalStatus,
            Address = x.Member.Address,
            NextOfKinPhoneNo = x.Member.NextOfKinPhoneNo,
            NextOfKinName = x.Member.NextOfKinName,
            RecordStatus = x.Member.RecordStatus,
            MemberShipStatus = x.Member.MemberShipStatus,
            PhotoUrl = x.Member.PhotoUrl,
            Signature = x.Member.Signature,
            BloodGroup = x.Member.BloodGroup,
            Genotype = x.Member.Genotype,
            JamaatId = x.Member.JamaatId,
            JamaatName = x.Jamaat?.Name,
            MuqamId = x.Jamaat?.MuqamId,
            MuqamName = x.Jamaat?.Muqam?.Name,
            DilaName = x.Jamaat?.Muqam?.Dila?.Name,
            ZoneName = x.Jamaat?.Muqam?.Dila?.Zone?.Name
        }).ToList();

        return new PaginationResponse<MemberDto>(
            memberDtos,
            totalCount,
            request.PageNumber,
            request.PageSize
        );
    }
}
