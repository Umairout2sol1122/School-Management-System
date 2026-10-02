using System.Security.Claims;
using SchoolManagement.Application.Common.Exceptions;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Api.Middleware;

public class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;

    public TenantResolutionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            string? tenantClaim = context.User.FindFirst("tenant_id")?.Value;
            string? branchClaim = context.User.FindFirst("branch_id")?.Value;
            string? roleClaim = context.User.FindFirst("role_type")?.Value;

            bool isSuperAdmin = string.Equals(roleClaim, nameof(UserRoleType.SuperAdmin), StringComparison.OrdinalIgnoreCase);

            Guid? orgId = Guid.TryParse(tenantClaim, out var parsedOrgId) ? parsedOrgId : null;
            Guid? branchId = Guid.TryParse(branchClaim, out var parsedBranchId) ? parsedBranchId : null;

            // Check if SuperAdmin supplied a target tenant header
            if (isSuperAdmin && context.Request.Headers.TryGetValue("X-Tenant-Id", out var targetTenantHeader))
            {
                if (Guid.TryParse(targetTenantHeader, out var headerOrgId))
                {
                    orgId = headerOrgId;
                }
            }
            else if (!isSuperAdmin && context.Request.Headers.TryGetValue("X-Tenant-Id", out var nonAdminHeader))
            {
                // Non-admin attempting to access another tenant's context via header
                if (Guid.TryParse(nonAdminHeader, out var spoofedOrgId) && spoofedOrgId != orgId)
                {
                    throw new CrossTenantAccessException(orgId ?? Guid.Empty, spoofedOrgId);
                }
            }

            tenantContext.SetTenant(orgId, branchId, isSuperAdmin);
        }

        await _next(context);
    }
}
