using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Api.Authorization;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.Common.Models;
using SchoolManagement.Domain.Constants;
using SchoolManagement.Domain.Entities.Security;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RolesController : BaseApiController
{
    private readonly IApplicationDbContext _context;
    private readonly IAuditService _auditService;

    public RolesController(IApplicationDbContext context, IAuditService auditService)
    {
        _context = context;
        _auditService = auditService;
    }

    [HttpGet]
    [RequirePermission(Permissions.RolesView)]
    public async Task<IActionResult> GetRoles([FromQuery] Guid? organizationId, CancellationToken cancellationToken)
    {
        Guid targetOrgId = CurrentUserService.IsSuperAdmin && organizationId.HasValue
            ? organizationId.Value
            : (CurrentUserService.OrganizationId ?? Guid.Empty);

        var roles = await _context.Roles
            .Include(r => r.RolePermissions)
            .Where(r => r.IsSystemRole || r.OrganizationId == targetOrgId)
            .OrderBy(r => r.IsSystemRole ? 0 : 1)
            .ThenBy(r => r.Name)
            .Select(r => new RoleDto(
                r.Id,
                r.Name,
                r.Description,
                r.IsSystemRole,
                r.RolePermissions.Count
            ))
            .ToListAsync(cancellationToken);

        return ApiSuccess(roles);
    }

    [HttpGet("permissions")]
    [RequirePermission(Permissions.RolesView)]
    public async Task<IActionResult> GetAllPermissions(CancellationToken cancellationToken)
    {
        List<string>? allowedModules = null;
        if (!CurrentUserService.IsSuperAdmin)
        {
            var orgId = CurrentUserService.OrganizationId;
            if (orgId.HasValue && orgId.Value != Guid.Empty)
            {
                allowedModules = await _context.OrganizationModules
                    .IgnoreQueryFilters()
                    .Where(m => m.OrganizationId == orgId.Value && m.IsEnabled && !m.IsDeleted)
                    .Select(m => m.ModuleName)
                    .ToListAsync(cancellationToken);
            }
        }

        var query = _context.Permissions.AsQueryable();
        if (allowedModules != null)
        {
            query = query.Where(p => allowedModules.Contains(p.Module));
        }

        var permissions = await query
            .OrderBy(p => p.Module)
            .ThenBy(p => p.Name)
            .Select(p => new PermissionDto(
                p.Id,
                p.Code,
                p.Name,
                p.Module,
                p.Description
            ))
            .ToListAsync(cancellationToken);

        var grouped = permissions
            .GroupBy(p => p.Module)
            .Select(g => new PermissionGroupDto(g.Key, g.ToList()))
            .ToList();

        return ApiSuccess(grouped);
    }

    [HttpGet("{id:guid}")]
    [RequirePermission(Permissions.RolesView)]
    public async Task<IActionResult> GetRoleById(Guid id, CancellationToken cancellationToken)
    {
        var role = await _context.Roles
            .Include(r => r.RolePermissions)
                .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        if (role == null)
            return ApiError("Role not found.", StatusCodes.Status404NotFound);

        if (!role.IsSystemRole && !CurrentUserService.IsSuperAdmin && role.OrganizationId != CurrentUserService.OrganizationId)
            return ApiError("Unauthorized access to organization role.", StatusCodes.Status403Forbidden);

        var permissions = role.RolePermissions
            .Where(rp => rp.Permission != null)
            .Select(rp => new PermissionDto(
                rp.Permission!.Id,
                rp.Permission.Code,
                rp.Permission.Name,
                rp.Permission.Module,
                rp.Permission.Description
            ))
            .ToList();

        var result = new RoleDetailsDto(
            role.Id,
            role.Name,
            role.Description,
            role.IsSystemRole,
            permissions
        );

        return ApiSuccess(result);
    }

    [HttpPost]
    [RequirePermission(Permissions.RolesManage)]
    public async Task<IActionResult> CreateRole([FromBody] CreateRoleRequestDto request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return ApiError("Role name is required.");

        Guid orgId = CurrentUserService.OrganizationId ?? Guid.Empty;
        if (orgId == Guid.Empty && !CurrentUserService.IsSuperAdmin)
            return ApiError("Tenant context required.", StatusCodes.Status400BadRequest);

        bool nameExists = await _context.Roles.AnyAsync(r =>
            (r.OrganizationId == orgId || r.IsSystemRole) &&
            r.Name.ToLower() == request.Name.Trim().ToLower(),
            cancellationToken);

        if (nameExists)
            return ApiError($"A role with the name '{request.Name}' already exists.");

        var role = new Role
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Description = request.Description?.Trim() ?? string.Empty,
            IsSystemRole = false,
            OrganizationId = orgId
        };

        if (request.PermissionIds != null && request.PermissionIds.Count > 0)
        {
            List<string>? allowedModules = null;
            if (!CurrentUserService.IsSuperAdmin)
            {
                allowedModules = await _context.OrganizationModules
                    .IgnoreQueryFilters()
                    .Where(m => m.OrganizationId == orgId && m.IsEnabled && !m.IsDeleted)
                    .Select(m => m.ModuleName)
                    .ToListAsync(cancellationToken);
            }

            var permQuery = _context.Permissions.Where(p => request.PermissionIds.Contains(p.Id));
            if (allowedModules != null)
            {
                permQuery = permQuery.Where(p => allowedModules.Contains(p.Module));
            }

            var validPermissions = await permQuery
                .Select(p => p.Id)
                .ToListAsync(cancellationToken);

            foreach (var permId in validPermissions)
            {
                role.RolePermissions.Add(new RolePermission
                {
                    Id = Guid.NewGuid(),
                    RoleId = role.Id,
                    PermissionId = permId
                });
            }
        }

        _context.Roles.Add(role);
        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            AuditAction.RoleChange, "RBAC", "Role", role.Id.ToString(),
            $"Created custom role '{role.Name}' with {role.RolePermissions.Count} permissions",
            organizationId: orgId,
            userId: CurrentUserService.UserId,
            userEmail: CurrentUserService.Email,
            cancellationToken: cancellationToken);

        return ApiSuccess(new RoleDto(role.Id, role.Name, role.Description, role.IsSystemRole, role.RolePermissions.Count),
            "Role created successfully.");
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(Permissions.RolesManage)]
    public async Task<IActionResult> UpdateRole(Guid id, [FromBody] UpdateRoleRequestDto request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return ApiError("Role name is required.");

        var role = await _context.Roles
            .Include(r => r.RolePermissions)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        if (role == null)
            return ApiError("Role not found.", StatusCodes.Status404NotFound);

        if (role.IsSystemRole)
            return ApiError("System roles cannot be modified directly.", StatusCodes.Status400BadRequest);

        if (!CurrentUserService.IsSuperAdmin && role.OrganizationId != CurrentUserService.OrganizationId)
            return ApiError("Unauthorized access to organization role.", StatusCodes.Status403Forbidden);

        bool nameExists = await _context.Roles.AnyAsync(r =>
            r.Id != id &&
            r.OrganizationId == role.OrganizationId &&
            r.Name.ToLower() == request.Name.Trim().ToLower(),
            cancellationToken);

        if (nameExists)
            return ApiError($"Another role with the name '{request.Name}' already exists.");

        role.Name = request.Name.Trim();
        role.Description = request.Description?.Trim() ?? string.Empty;

        // Synchronize permissions
        role.RolePermissions.Clear();
        if (request.PermissionIds != null && request.PermissionIds.Count > 0)
        {
            List<string>? allowedModules = null;
            if (!CurrentUserService.IsSuperAdmin)
            {
                allowedModules = await _context.OrganizationModules
                    .IgnoreQueryFilters()
                    .Where(m => m.OrganizationId == role.OrganizationId && m.IsEnabled && !m.IsDeleted)
                    .Select(m => m.ModuleName)
                    .ToListAsync(cancellationToken);
            }

            var permQuery = _context.Permissions.Where(p => request.PermissionIds.Contains(p.Id));
            if (allowedModules != null)
            {
                permQuery = permQuery.Where(p => allowedModules.Contains(p.Module));
            }

            var validPermissions = await permQuery
                .Select(p => p.Id)
                .ToListAsync(cancellationToken);

            foreach (var permId in validPermissions)
            {
                role.RolePermissions.Add(new RolePermission
                {
                    Id = Guid.NewGuid(),
                    RoleId = role.Id,
                    PermissionId = permId
                });
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            AuditAction.RoleChange, "RBAC", "Role", role.Id.ToString(),
            $"Updated role '{role.Name}' permissions (Total: {role.RolePermissions.Count})",
            organizationId: role.OrganizationId,
            userId: CurrentUserService.UserId,
            userEmail: CurrentUserService.Email,
            cancellationToken: cancellationToken);

        return ApiSuccess("Role updated successfully.");
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(Permissions.RolesManage)]
    public async Task<IActionResult> DeleteRole(Guid id, CancellationToken cancellationToken)
    {
        var role = await _context.Roles
            .Include(r => r.UserRoles)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        if (role == null)
            return ApiError("Role not found.", StatusCodes.Status404NotFound);

        if (role.IsSystemRole)
            return ApiError("System roles cannot be deleted.", StatusCodes.Status400BadRequest);

        if (!CurrentUserService.IsSuperAdmin && role.OrganizationId != CurrentUserService.OrganizationId)
            return ApiError("Unauthorized access to organization role.", StatusCodes.Status403Forbidden);

        if (role.UserRoles.Any())
            return ApiError("Cannot delete role because it is currently assigned to one or more users.", StatusCodes.Status400BadRequest);

        _context.Roles.Remove(role);
        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            AuditAction.RoleChange, "RBAC", "Role", role.Id.ToString(),
            $"Deleted custom role '{role.Name}'",
            organizationId: role.OrganizationId,
            userId: CurrentUserService.UserId,
            userEmail: CurrentUserService.Email,
            cancellationToken: cancellationToken);

        return ApiSuccess("Role deleted successfully.");
    }
}
