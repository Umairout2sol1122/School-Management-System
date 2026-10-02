using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Api.Authorization;

public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IApplicationDbContext? _context;

    public PermissionAuthorizationHandler(IApplicationDbContext? context = null)
    {
        _context = context;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (context.User?.Identity?.IsAuthenticated != true)
        {
            return;
        }

        // SuperAdmin has unconditional access across the entire platform
        if (context.User.IsInRole(UserRoleType.SuperAdmin.ToString()) ||
            context.User.HasClaim(c => c.Type == ClaimTypes.Role && c.Value == UserRoleType.SuperAdmin.ToString()) ||
            context.User.HasClaim(c => c.Type == "role_type" && c.Value == UserRoleType.SuperAdmin.ToString()))
        {
            context.Succeed(requirement);
            return;
        }

        // Check for specific permission claim
        var hasPermission = context.User.HasClaim(c =>
            (c.Type == "permission" || c.Type == "permissions") &&
            string.Equals(c.Value, requirement.Permission, StringComparison.OrdinalIgnoreCase));

        if (!hasPermission)
        {
            return;
        }

        // Enforce Organization Active Status and Module Access if DbContext available
        if (_context != null)
        {
            var tenantClaim = context.User.FindFirst("tenant_id")?.Value;
            if (Guid.TryParse(tenantClaim, out var orgId) && orgId != Guid.Empty)
            {
                // Verify Organization is active
                var org = await _context.Organizations
                    .IgnoreQueryFilters()
                    .Where(o => o.Id == orgId && !o.IsDeleted)
                    .Select(o => new { o.IsActive })
                    .FirstOrDefaultAsync();

                if (org == null || !org.IsActive)
                {
                    // Inactive or suspended organization cannot access protected operations
                    return;
                }

                // Extract module name from permission (e.g. "Fees.View" -> "Fees")
                var moduleName = requirement.Permission.Split('.')[0];
                if (!string.IsNullOrWhiteSpace(moduleName))
                {
                    var moduleSetting = await _context.OrganizationModules
                        .IgnoreQueryFilters()
                        .Where(m => m.OrganizationId == orgId && m.ModuleName == moduleName && !m.IsDeleted)
                        .Select(m => new { m.IsEnabled })
                        .FirstOrDefaultAsync();

                    // If explicitly disabled by SuperAdmin, block access
                    if (moduleSetting != null && !moduleSetting.IsEnabled)
                    {
                        return;
                    }
                }
            }
        }

        context.Succeed(requirement);
    }
}
