using FluentValidation;
using ManagementApi.Application.Auth.DTOs;
using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Application.Common.Models;
using ManagementApi.Domain.Entities;
using ManagementApi.Domain.Identity;
using ManagementApi.Shared.Authorization;
using Mapster;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ManagementApi.Application.Auth.Commands;

public record RegisterCommand(RegisterRequest Request) : IRequest<Result<AuthResponse>>;

public class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    public RegisterCommandValidator()
    {
        RuleFor(x => x.Request.ChandaNo)
            .NotEmpty().WithMessage("Membership number is required")
            .MaximumLength(50).WithMessage("Membership number must not exceed 50 characters");

        RuleFor(x => x.Request.Password)
            .NotEmpty().WithMessage("Password is required")
            .MinimumLength(3).WithMessage("Password must be at least 3 characters");

        RuleFor(x => x.Request.ConfirmPassword)
            .Equal(x => x.Request.Password).WithMessage("Passwords do not match");
    }
}

public class RegisterCommandHandler : IRequestHandler<RegisterCommand, Result<AuthResponse>>
{
    private readonly IIdentityService _identityService;
    private readonly ITokenService _tokenService;
    private readonly IRepository<Member> _memberRepository;
    private readonly IRepository<Jamaat> _jamaatRepository;
    private readonly IRepository<Muqam> _muqamRepository;
    private readonly IApplicationDbContext _context;

    public RegisterCommandHandler(
        IIdentityService identityService,
        ITokenService tokenService,
        IRepository<Member> memberRepository,
        IRepository<Jamaat> jamaatRepository,
        IRepository<Muqam> muqamRepository,
        IApplicationDbContext context)
    {
        _identityService = identityService;
        _tokenService = tokenService;
        _memberRepository = memberRepository;
        _jamaatRepository = jamaatRepository;
        _muqamRepository = muqamRepository;
        _context = context;
    }

    public async Task<Result<AuthResponse>> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        // 1. Check if user already exists
        var existingUser = await _identityService.GetUserByChandaNoAsync(request.Request.ChandaNo);
        if (existingUser != null)
        {
            return Result<AuthResponse>.Failure("A user with this membership number already exists");
        }

        // 2. Fetch member from Members table by ChandaNo
        var member = await _context.Members
            .FirstOrDefaultAsync(m => m.ChandaNo == request.Request.ChandaNo, cancellationToken);

        if (member == null)
        {
            return Result<AuthResponse>.Failure($"Member with membership number '{request.Request.ChandaNo}' not found");
        }

        // 3. Get organization info via Jamaat mapping (if JamaatId exists)
        Guid? muqamId = member.MuqamId;
        Guid? dilaId = null;
        Guid? zoneId = null;

        if (member.JamaatId.HasValue)
        {
            var jamaat = await _context.Jamaats
                .Include(j => j.Muqam)
                    .ThenInclude(m => m!.Dila)
                        .ThenInclude(d => d!.Zone)
                .FirstOrDefaultAsync(j => j.JamaatId == member.JamaatId.Value, cancellationToken);

            if (jamaat?.MuqamId != null)
            {
                muqamId = jamaat.MuqamId;

                var muqam = jamaat.Muqam;
                if (muqam != null)
                {
                    dilaId = muqam.DilaId;

                    if (muqam.Dila != null)
                    {
                        zoneId = muqam.Dila.ZoneId;
                    }
                }
            }
        }
        else if (muqamId.HasValue)
        {
            // Get Dila and Zone from Muqam
            var muqam = await _context.Muqams
                .Include(m => m.Dila)
                    .ThenInclude(d => d!.Zone)
                .FirstOrDefaultAsync(m => m.Id == muqamId.Value, cancellationToken);

            if (muqam != null)
            {
                dilaId = muqam.DilaId;

                if (muqam.Dila != null)
                {
                    zoneId = muqam.Dila.ZoneId;
                }
            }
        }

        // 4. Create user with member data
        var user = new ApplicationUser
        {
            UserName = request.Request.ChandaNo,
            Email = member.Email ?? $"{request.Request.ChandaNo}@temp.local",
            ChandaNo = request.Request.ChandaNo,
            MemberId = member.Id,
            FirstName = member.FirstName,
            LastName = member.Surname,
            ImageUrl = member.PhotoUrl,
            IsActive = true,
            MuqamId = muqamId,
            DilaId = dilaId,
            ZoneId = zoneId,
            OrganizationLevel = DetermineOrganizationLevel(muqamId, dilaId, zoneId)
        };

        var createResult = await _identityService.CreateUserAsync(user, request.Request.Password);

        if (!createResult.Succeeded)
        {
            return Result<AuthResponse>.Failure(createResult.Messages);
        }

        // 5. Assign default "Member" role
        var roleResult = await _identityService.AssignRolesToUserAsync(user.Id, new List<string> { ManagementApi.Shared.Authorization.Roles.Member });

        if (!roleResult.Succeeded)
        {
            return Result<AuthResponse>.Failure(roleResult.Messages);
        }

        // 6. Generate JWT token
        var token = await _tokenService.GenerateTokenAsync(user);

        // 7. Get user roles and permissions for response
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

        return Result<AuthResponse>.Success(authResponse, "Registration successful");
    }

    private static Domain.Enums.OrganizationLevel? DetermineOrganizationLevel(Guid? muqamId, Guid? dilaId, Guid? zoneId)
    {
        if (zoneId.HasValue) return Domain.Enums.OrganizationLevel.Zone;
        if (dilaId.HasValue) return Domain.Enums.OrganizationLevel.Dila;
        if (muqamId.HasValue) return Domain.Enums.OrganizationLevel.Muqam;
        return null;
    }
}
