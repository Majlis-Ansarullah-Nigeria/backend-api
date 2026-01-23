using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Application.Common.Models;
using ManagementApi.Application.Members.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ManagementApi.Application.Members.Queries;

public record SearchMembersQuery(SearchMembersRequest Request) : IRequest<PaginationResponse<MemberDto>>;

public class SearchMembersQueryHandler : IRequestHandler<SearchMembersQuery, PaginationResponse<MemberDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public SearchMembersQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<PaginationResponse<MemberDto>> Handle(SearchMembersQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Members
            .Include(m => m.Muqam)
                .ThenInclude(m => m!.Dila)
                    .ThenInclude(d => d!.Zone)
            .AsQueryable();

        // SECURITY: First apply automatic filtering based on current user's organization level
        // This ensures users can only see data within their organizational scope
        var orgLevel = _currentUser.OrganizationLevel;

        switch (orgLevel)
        {
            case ManagementApi.Domain.Enums.OrganizationLevel.Muqam:
                if (_currentUser.MuqamId.HasValue)
                {
                    var muqamJamaatIds = _context.Jamaats
                        .Where(j => j.MuqamId == _currentUser.MuqamId.Value)
                        .Select(j => j.JamaatId)
                        .ToList();

                    query = query.Where(m => m.JamaatId.HasValue && muqamJamaatIds.Contains(m.JamaatId.Value));
                }
                break;

            case ManagementApi.Domain.Enums.OrganizationLevel.Dila:
                if (_currentUser.DilaId.HasValue)
                {
                    var dilaJamaatIds = _context.Jamaats
                        .Where(j => j.Muqam != null && j.Muqam.DilaId == _currentUser.DilaId.Value)
                        .Select(j => j.JamaatId)
                        .ToList();

                    query = query.Where(m => m.JamaatId.HasValue && dilaJamaatIds.Contains(m.JamaatId.Value));
                }
                break;

            case ManagementApi.Domain.Enums.OrganizationLevel.Zone:
                if (_currentUser.ZoneId.HasValue)
                {
                    var zoneJamaatIds = _context.Jamaats
                        .Where(j => j.Muqam != null &&
                                   j.Muqam.Dila != null &&
                                   j.Muqam.Dila.ZoneId == _currentUser.ZoneId.Value)
                        .Select(j => j.JamaatId)
                        .ToList();

                    query = query.Where(m => m.JamaatId.HasValue && zoneJamaatIds.Contains(m.JamaatId.Value));
                }
                break;

            case ManagementApi.Domain.Enums.OrganizationLevel.National:
                // National level - no automatic filtering
                break;
        }

        // Then apply additional filters from request (within user's scope)
        if (request.Request.MuqamId.HasValue)
        {
            var muqamJamaatIds = _context.Jamaats
                .Where(j => j.MuqamId == request.Request.MuqamId.Value)
                .Select(j => j.JamaatId)
                .ToList();

            query = query.Where(m => m.JamaatId.HasValue && muqamJamaatIds.Contains(m.JamaatId.Value));
        }
        else if (request.Request.DilaId.HasValue)
        {
            var dilaJamaatIds = _context.Jamaats
                .Where(j => j.Muqam != null && j.Muqam.DilaId == request.Request.DilaId.Value)
                .Select(j => j.JamaatId)
                .ToList();

            query = query.Where(m => m.JamaatId.HasValue && dilaJamaatIds.Contains(m.JamaatId.Value));
        }
        else if (request.Request.ZoneId.HasValue)
        {
            var zoneJamaatIds = _context.Jamaats
                .Where(j => j.Muqam != null &&
                           j.Muqam.Dila != null &&
                           j.Muqam.Dila.ZoneId == request.Request.ZoneId.Value)
                .Select(j => j.JamaatId)
                .ToList();

            query = query.Where(m => m.JamaatId.HasValue && zoneJamaatIds.Contains(m.JamaatId.Value));
        }

        // Search term filter
        if (!string.IsNullOrWhiteSpace(request.Request.SearchTerm))
        {
            var searchTerm = request.Request.SearchTerm.ToLower();
            query = query.Where(m =>
                m.ChandaNo.ToLower().Contains(searchTerm) ||
                m.FirstName!.ToLower().Contains(searchTerm) ||
                m.Surname.ToLower().Contains(searchTerm) ||
                m.Email!.ToLower().Contains(searchTerm));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var members = await query
            .OrderBy(m => m.Surname)
            .Skip((request.Request.PageNumber - 1) * request.Request.PageSize)
            .Take(request.Request.PageSize)
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
            request.Request.PageNumber,
            request.Request.PageSize
        );
    }
}
