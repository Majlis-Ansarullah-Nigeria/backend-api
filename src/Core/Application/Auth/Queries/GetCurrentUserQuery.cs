using ManagementApi.Application.Auth.DTOs;
using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ManagementApi.Application.Auth.Queries;

public record GetCurrentUserQuery : IRequest<Result<UserDto>>;

public class GetCurrentUserQueryHandler : IRequestHandler<GetCurrentUserQuery, Result<UserDto>>
{
    private readonly ICurrentUser _currentUser;
    private readonly IIdentityService _identityService;
    private readonly IApplicationDbContext _context;

    public GetCurrentUserQueryHandler(
        ICurrentUser currentUser,
        IIdentityService identityService,
        IApplicationDbContext context)
    {
        _currentUser = currentUser;
        _identityService = identityService;
        _context = context;
    }

    public async Task<Result<UserDto>> Handle(GetCurrentUserQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.GetUserId();

        if (!userId.HasValue)
        {
            return Result<UserDto>.Failure("User not authenticated");
        }

        var user = await _identityService.GetUserByIdAsync(userId.Value);

        if (user == null)
        {
            return Result<UserDto>.Failure("User not found");
        }

        var roles = await _identityService.GetUserRolesAsync(user);
        var permissions = await _identityService.GetUserPermissionsAsync(user);

        var userDto = new UserDto
        {
            Id = user.Id,
            ChandaNo = user.ChandaNo,
            MemberId = user.MemberId,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email,
            ImageUrl = user.ImageUrl,
            IsActive = user.IsActive,
            MuqamId = user.MuqamId,
            DilaId = user.DilaId,
            ZoneId = user.ZoneId,
            OrganizationLevel = user.OrganizationLevel,
            // Unit names for greetings such as "Nazim Ala, Ikeja Dila · Lagos Zone"
            MuqamName = user.MuqamId is { } muqamId
                ? await _context.Muqams.Where(m => m.Id == muqamId).Select(m => m.Name).FirstOrDefaultAsync(cancellationToken) : null,
            DilaName = user.DilaId is { } dilaId
                ? await _context.Dilas.Where(d => d.Id == dilaId).Select(d => d.Name).FirstOrDefaultAsync(cancellationToken) : null,
            ZoneName = user.ZoneId is { } zoneId
                ? await _context.Zones.Where(z => z.Id == zoneId).Select(z => z.Name).FirstOrDefaultAsync(cancellationToken) : null,
            Roles = roles,
            Permissions = permissions
        };

        return Result<UserDto>.Success(userDto);
    }
}
