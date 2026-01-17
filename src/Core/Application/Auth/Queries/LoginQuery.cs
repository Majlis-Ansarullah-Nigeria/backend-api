using FluentValidation;
using ManagementApi.Application.Auth.DTOs;
using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Application.Common.Models;
using MediatR;

namespace ManagementApi.Application.Auth.Queries;

public record LoginQuery(LoginRequest Request) : IRequest<Result<AuthResponse>>;

public class LoginQueryValidator : AbstractValidator<LoginQuery>
{
    public LoginQueryValidator()
    {
        RuleFor(x => x.Request.ChandaNo)
            .NotEmpty().WithMessage("Membership number is required");

        RuleFor(x => x.Request.Password)
            .NotEmpty().WithMessage("Password is required");
    }
}

public class LoginQueryHandler : IRequestHandler<LoginQuery, Result<AuthResponse>>
{
    private readonly IIdentityService _identityService;
    private readonly ITokenService _tokenService;

    public LoginQueryHandler(
        IIdentityService identityService,
        ITokenService tokenService)
    {
        _identityService = identityService;
        _tokenService = tokenService;
    }

    public async Task<Result<AuthResponse>> Handle(LoginQuery request, CancellationToken cancellationToken)
    {
        // 1. Find user by ChandaNo
        var user = await _identityService.GetUserByChandaNoAsync(request.Request.ChandaNo);

        if (user == null)
        {
            return Result<AuthResponse>.Failure("Invalid membership number or password");
        }

        // 2. Check if user is active
        if (!user.IsActive)
        {
            return Result<AuthResponse>.Failure("Your account has been deactivated. Please contact administrator");
        }

        // 3. Verify password
        var isPasswordValid = await _identityService.CheckPasswordAsync(user, request.Request.Password);

        if (!isPasswordValid)
        {
            return Result<AuthResponse>.Failure("Invalid membership number or password");
        }

        // 4. Generate JWT token
        var token = await _tokenService.GenerateTokenAsync(user);

        // 5. Get user roles and permissions
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

        var authResponse = new AuthResponse
        {
            Token = token,
            User = userDto
        };

        return Result<AuthResponse>.Success(authResponse, "Login successful");
    }
}
