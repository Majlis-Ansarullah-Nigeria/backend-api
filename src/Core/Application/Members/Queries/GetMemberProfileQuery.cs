using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Application.Common.Models;
using ManagementApi.Application.Members.DTOs;
using ManagementApi.Shared.Authorization;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Roles = ManagementApi.Shared.Authorization.Roles;

namespace ManagementApi.Application.Members.Queries;

public record GetMemberProfileQuery(string ChandaNo) : IRequest<Result<MemberDto>>;

public class GetMemberProfileQueryHandler : IRequestHandler<GetMemberProfileQuery, Result<MemberDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetMemberProfileQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<MemberDto>> Handle(GetMemberProfileQuery request, CancellationToken cancellationToken)
    {
        // Find member by ChandaNo
        var member = await _context.Members
            .Include(m => m.Muqam)
                .ThenInclude(m => m!.Dila)
                    .ThenInclude(d => d!.Zone)
            .FirstOrDefaultAsync(m => m.ChandaNo == request.ChandaNo, cancellationToken);

        if (member == null)
        {
            return Result<MemberDto>.Failure($"Member with Chanda Number '{request.ChandaNo}' not found");
        }

        // SECURITY: Check if user is authorized to view this profile
        var canViewProfile = await CanViewProfile(member.ChandaNo, member.JamaatId, cancellationToken);

        if (!canViewProfile)
        {
            return Result<MemberDto>.Failure("You are not authorized to view this profile");
        }

        // Get Jamaat details if exists
        var jamaat = member.JamaatId.HasValue
            ? await _context.Jamaats
                .Include(j => j.Muqam)
                    .ThenInclude(mu => mu!.Dila)
                        .ThenInclude(d => d!.Zone)
                .FirstOrDefaultAsync(j => j.JamaatId == member.JamaatId.Value, cancellationToken)
            : null;

        var memberDto = new MemberDto
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
            JamaatId = member.JamaatId,
            JamaatName = jamaat?.Name,
            MuqamId = jamaat?.MuqamId,
            MuqamName = jamaat?.Muqam?.Name,
            DilaName = jamaat?.Muqam?.Dila?.Name,
            ZoneName = jamaat?.Muqam?.Dila?.Zone?.Name
        };

        return Result<MemberDto>.Success(memberDto);
    }

    /// <summary>
    /// Determines if current user can view the specified member profile
    /// Rules:
    /// - Users with "Member" role can ONLY view their own profile
    /// - Users with elevated roles (ZaimAla, NazimAla, ZoneNazim, etc.) can view profiles within their organizational scope
    /// - SuperAdmin and NationalAdmin can view any profile
    /// </summary>
    private async Task<bool> CanViewProfile(string memberChandaNo, int? jamaatId, CancellationToken cancellationToken)
    {
        // SuperAdmin and NationalAdmin can view any profile
        if (_currentUser.IsInRole(ManagementApi.Shared.Authorization.Roles.SuperAdmin) ||
            _currentUser.IsInRole(ManagementApi.Shared.Authorization.Roles.NationalAdmin))
        {
            return true;
        }

        // Check if user is viewing their own profile
        if (!string.IsNullOrEmpty(_currentUser.ChandaNo) && _currentUser.ChandaNo == memberChandaNo)
        {
            return true;
        }

        // Users with only "Member" role can ONLY view their own profile
        if (_currentUser.IsInRole(ManagementApi.Shared.Authorization.Roles.Member) &&
            !_currentUser.IsInRole(ManagementApi.Shared.Authorization.Roles.ZaimAla) &&
            !_currentUser.IsInRole(ManagementApi.Shared.Authorization.Roles.NazimAla) &&
            !_currentUser.IsInRole(ManagementApi.Shared.Authorization.Roles.ZoneNazim))
        {
            return false; // Already checked if it's their own profile above
        }

        // For users with elevated roles, check organizational scope
        if (!jamaatId.HasValue)
        {
            return false; // Member has no Jamaat association
        }

        var jamaat = await _context.Jamaats
            .Include(j => j.Muqam)
                .ThenInclude(m => m!.Dila)
            .FirstOrDefaultAsync(j => j.JamaatId == jamaatId.Value, cancellationToken);

        if (jamaat?.Muqam == null)
        {
            return false;
        }

        // Check based on organization level
        switch (_currentUser.OrganizationLevel)
        {
            case ManagementApi.Domain.Enums.OrganizationLevel.Muqam:
                // ZaimAla can view profiles in their Muqam
                if (_currentUser.MuqamId.HasValue)
                {
                    return jamaat.MuqamId.HasValue && jamaat.MuqamId.Value == _currentUser.MuqamId.Value;
                }
                break;

            case ManagementApi.Domain.Enums.OrganizationLevel.Dila:
                // NazimAla can view profiles in their Dila
                if (_currentUser.DilaId.HasValue)
                {
                    return jamaat.Muqam.DilaId == _currentUser.DilaId.Value;
                }
                break;

            case ManagementApi.Domain.Enums.OrganizationLevel.Zone:
                // ZoneNazim can view profiles in their Zone
                if (_currentUser.ZoneId.HasValue && jamaat.Muqam.Dila != null)
                {
                    return jamaat.Muqam.Dila.ZoneId == _currentUser.ZoneId.Value;
                }
                break;

            case ManagementApi.Domain.Enums.OrganizationLevel.National:
                // National level can view all
                return true;
        }

        return false;
    }
}
