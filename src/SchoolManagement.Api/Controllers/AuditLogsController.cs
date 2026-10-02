using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Api.Authorization;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.Common.Models;
using SchoolManagement.Domain.Constants;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Api.Controllers;

[ApiController]
[Route("api/audit-logs")]
[Authorize]
public class AuditLogsController : BaseApiController
{
    private readonly IApplicationDbContext _context;

    public AuditLogsController(IApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    [RequirePermission(Permissions.AuditView)]
    public async Task<IActionResult> GetAuditLogs(
        [FromQuery] DateTime? fromDateUtc,
        [FromQuery] DateTime? toDateUtc,
        [FromQuery] AuditAction? action,
        [FromQuery] string? module,
        [FromQuery] Guid? organizationId,
        [FromQuery] Guid? branchId,
        [FromQuery] string? search,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = _context.AuditLogs.AsQueryable();

        // Multi-tenant scoping
        if (!CurrentUserService.IsSuperAdmin)
        {
            if (CurrentUserService.OrganizationId == null)
                return ApiError("Tenant context required.", StatusCodes.Status403Forbidden);

            query = query.Where(a => a.OrganizationId == CurrentUserService.OrganizationId.Value);

            // Branch administrator (Principal) can only see their own branch's audit logs
            if (CurrentUserService.BranchId.HasValue && CurrentUserService.RoleType == UserRoleType.Principal)
            {
                query = query.Where(a => a.BranchId == CurrentUserService.BranchId.Value);
            }
            else if (branchId.HasValue)
            {
                query = query.Where(a => a.BranchId == branchId.Value);
            }
        }
        else
        {
            if (organizationId.HasValue)
            {
                query = query.Where(a => a.OrganizationId == organizationId.Value);
            }
            if (branchId.HasValue)
            {
                query = query.Where(a => a.BranchId == branchId.Value);
            }
        }

        if (fromDateUtc.HasValue)
        {
            query = query.Where(a => a.TimestampUtc >= fromDateUtc.Value);
        }

        if (toDateUtc.HasValue)
        {
            query = query.Where(a => a.TimestampUtc <= toDateUtc.Value);
        }

        if (action.HasValue)
        {
            query = query.Where(a => a.Action == action.Value);
        }

        if (!string.IsNullOrWhiteSpace(module))
        {
            query = query.Where(a => a.Module.ToLower() == module.Trim().ToLower());
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            string term = search.Trim().ToLower();
            query = query.Where(a =>
                a.Details.ToLower().Contains(term) ||
                a.UserEmail.ToLower().Contains(term) ||
                a.EntityName.ToLower().Contains(term));
        }

        int totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(a => a.TimestampUtc)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new AuditLogSummaryDto(
                a.Id,
                a.TimestampUtc,
                a.Action,
                a.Module,
                a.EntityName,
                a.EntityId,
                a.Details,
                a.IpAddress,
                a.CorrelationId,
                a.OrganizationId,
                null,
                a.UserEmail
            ))
            .ToListAsync(cancellationToken);

        return ApiSuccess(new
        {
            items,
            pageNumber,
            pageSize,
            totalCount,
            totalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
        });
    }
}
