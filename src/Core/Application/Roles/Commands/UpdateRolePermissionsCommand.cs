using FluentValidation;
using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Application.Common.Models;
using ManagementApi.Domain.Identity;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ManagementApi.Application.Roles.Commands;

public record UpdateRolePermissionsCommand(string RoleName, List<string> Permissions) : IRequest<Result>;

public class UpdateRolePermissionsCommandValidator : AbstractValidator<UpdateRolePermissionsCommand>
{
    public UpdateRolePermissionsCommandValidator()
    {
        RuleFor(x => x.RoleName)
            .NotEmpty().WithMessage("Role name is required");

        RuleFor(x => x.Permissions)
            .NotNull().WithMessage("Permissions list cannot be null");
    }
}

public class UpdateRolePermissionsCommandHandler : IRequestHandler<UpdateRolePermissionsCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly RoleManager<ApplicationRole> _roleManager;

    public UpdateRolePermissionsCommandHandler(
        IApplicationDbContext context,
        RoleManager<ApplicationRole> roleManager)
    {
        _context = context;
        _roleManager = roleManager;
    }

    public async Task<Result> Handle(UpdateRolePermissionsCommand request, CancellationToken cancellationToken)
    {
        // Verify the role exists
        var role = await _roleManager.FindByNameAsync(request.RoleName);
        if (role == null)
        {
            return Result.Failure($"Role '{request.RoleName}' not found");
        }

        // Get all existing permissions for this role
        var existingRolePermissions = await _context.RolePermissions
            .Where(rp => rp.RoleId == role.Id)
            .ToListAsync(cancellationToken);

        // Find permissions to remove (exist in DB but not in request)
        var permissionsToRemove = existingRolePermissions
            .Where(rp => !request.Permissions.Contains(rp.Permission))
            .ToList();

        // Find permissions to add (exist in request but not in DB)
        var existingPermissionNames = existingRolePermissions.Select(rp => rp.Permission).ToList();
        var permissionsToAdd = request.Permissions
            .Where(p => !existingPermissionNames.Contains(p))
            .ToList();

        // Remove permissions
        if (permissionsToRemove.Any())
        {
            _context.RolePermissions.RemoveRange(permissionsToRemove);
        }

        // Add new permissions
        if (permissionsToAdd.Any())
        {
            var newRolePermissions = permissionsToAdd.Select(permission => new ApplicationRolePermission
            {
                RoleId = role.Id,
                Permission = permission
            }).ToList();

            await _context.RolePermissions.AddRangeAsync(newRolePermissions, cancellationToken);
        }

        // Save changes
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success($"Permissions updated successfully for role '{request.RoleName}'");
    }
}