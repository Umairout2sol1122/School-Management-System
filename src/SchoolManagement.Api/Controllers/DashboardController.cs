using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.Common.Models;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DashboardController : BaseApiController
{
    private readonly IApplicationDbContext _context;

    public DashboardController(IApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet("summary")]
    [HttpGet("stats")]
    public async Task<IActionResult> GetSummary(CancellationToken cancellationToken)
    {
        if (CurrentUserService.IsSuperAdmin)
        {
            int totalOrgs = await _context.Organizations.CountAsync(o => !o.IsDeleted, cancellationToken);
            int totalBranches = await _context.Branches.CountAsync(b => !b.IsDeleted, cancellationToken);
            int totalUsers = await _context.Users.CountAsync(u => !u.IsDeleted, cancellationToken);
            int totalStaff = await _context.StaffProfiles.CountAsync(s => !s.IsDeleted, cancellationToken);
            if (totalStaff == 0)
            {
                totalStaff = await _context.Users.CountAsync(u => !u.IsDeleted && (u.RoleType == UserRoleType.Teacher || u.RoleType == UserRoleType.Staff || u.RoleType == UserRoleType.Principal || u.RoleType == UserRoleType.OrganizationAdmin), cancellationToken);
            }

            int totalStudents = await _context.Students.CountAsync(s => !s.IsDeleted, cancellationToken);
            if (totalStudents == 0)
            {
                totalStudents = await _context.Users.CountAsync(u => !u.IsDeleted && u.RoleType == UserRoleType.Student, cancellationToken);
            }

            var recentLogs = await _context.AuditLogs
                .OrderByDescending(a => a.CreatedAtUtc)
                .Take(10)
                .Select(a => new RecentActivityDto(
                    a.Id,
                    a.Action.ToString(),
                    a.Module,
                    a.EntityName,
                    a.Details,
                    a.UserEmail,
                    a.TimestampUtc
                ))
                .ToListAsync(cancellationToken);

            var summary = new DashboardSummaryDto(
                true,
                null,
                "Master Platform",
                null,
                "All Campuses",
                totalOrgs,
                totalBranches,
                totalUsers,
                totalStaff,
                totalStudents,
                recentLogs
            );

            return ApiSuccess(summary);
        }

        Guid? orgId = CurrentUserService.OrganizationId;
        if (!orgId.HasValue || orgId.Value == Guid.Empty)
        {
            return ApiError("Tenant context required.", StatusCodes.Status400BadRequest);
        }

        var org = await _context.Organizations
            .FirstOrDefaultAsync(o => o.Id == orgId.Value, cancellationToken);

        if (org == null)
        {
            return ApiError("Organization not found.", StatusCodes.Status404NotFound);
        }

        string? branchName = null;
        if (CurrentUserService.BranchId.HasValue)
        {
            var branch = await _context.Branches
                .FirstOrDefaultAsync(b => b.Id == CurrentUserService.BranchId.Value, cancellationToken);
            branchName = branch?.Name;
        }

        int branchesCount = await _context.Branches
            .CountAsync(b => b.OrganizationId == orgId.Value && !b.IsDeleted, cancellationToken);

        int usersCount = await _context.Users
            .CountAsync(u => u.OrganizationId == orgId.Value && !u.IsDeleted, cancellationToken);

        int staffCount = await _context.StaffProfiles
            .CountAsync(s => s.OrganizationId == orgId.Value && !s.IsDeleted, cancellationToken);
        if (staffCount == 0)
        {
            staffCount = await _context.Users.CountAsync(u => u.OrganizationId == orgId.Value && !u.IsDeleted && (u.RoleType == UserRoleType.Teacher || u.RoleType == UserRoleType.Staff), cancellationToken);
        }

        int studentsCount = await _context.Students
            .CountAsync(s => s.OrganizationId == orgId.Value && !s.IsDeleted, cancellationToken);
        if (studentsCount == 0)
        {
            studentsCount = await _context.Users.CountAsync(u => u.OrganizationId == orgId.Value && !u.IsDeleted && u.RoleType == UserRoleType.Student, cancellationToken);
        }

        int classesCount = await _context.ClassGrades
            .CountAsync(c => c.OrganizationId == orgId.Value && !c.IsDeleted, cancellationToken);

        int departmentsCount = await _context.Departments
            .CountAsync(d => d.OrganizationId == orgId.Value && !d.IsDeleted, cancellationToken);

        var tenantRecentLogs = await _context.AuditLogs
            .Where(a => a.OrganizationId == orgId.Value)
            .OrderByDescending(a => a.CreatedAtUtc)
            .Take(10)
            .Select(a => new RecentActivityDto(
                a.Id,
                a.Action.ToString(),
                a.Module,
                a.EntityName,
                a.Details,
                a.UserEmail,
                a.TimestampUtc
            ))
            .ToListAsync(cancellationToken);

        var tenantSummary = new DashboardSummaryDto(
            false,
            org.Id,
            org.Name,
            CurrentUserService.BranchId,
            branchName ?? "Main Campus",
            1,
            branchesCount,
            usersCount,
            staffCount,
            studentsCount,
            tenantRecentLogs,
            classesCount,
            departmentsCount
        );

        return ApiSuccess(tenantSummary);
    }
}
