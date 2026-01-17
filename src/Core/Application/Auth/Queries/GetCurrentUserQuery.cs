using ManagementApi.Application.Auth.DTOs;
using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Application.Common.Models;
using MediatR;

namespace ManagementApi.Application.Auth.Queries;

public record GetCurrentUserQuery : IRequest<Result<UserDto>>;

public class GetCurrentUserQueryHandler : IRequestHandler<GetCurrentUserQuery, Result<UserDto>>
{
    private readonly ICurrentUser _currentUser;
    private readonly IIdentityService _identityService;

    public GetCurrentUserQueryHandler(
        ICurrentUser currentUser,
        IIdentityService identityService)
    {
        _currentUser = currentUser;
        _identityService = identityService;
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
            Roles = roles,
            Permissions = permissions
        };

        return Result<UserDto>.Success(userDto);
    }
}
