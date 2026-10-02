using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Api.Authorization;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.Common.Models;
using SchoolManagement.Domain.Constants;
using SchoolManagement.Domain.Entities.Tenant;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BranchesController : BaseApiController
{
    private readonly IApplicationDbContext _context;
    private readonly IAuditService _auditService;

    public BranchesController(IApplicationDbContext context, IAuditService auditService)
    {
        _context = context;
        _auditService = auditService;
    }

    [HttpGet]
    [RequirePermission(Permissions.BranchesView)]
    public async Task<IActionResult> GetBranches([FromQuery] Guid? organizationId, [FromQuery] bool? isActive, CancellationToken cancellationToken)
    {
        Guid targetOrgId = CurrentUserService.IsSuperAdmin && organizationId.HasValue
            ? organizationId.Value
            : (CurrentUserService.OrganizationId ?? Guid.Empty);

        if (targetOrgId == Guid.Empty && !CurrentUserService.IsSuperAdmin)
        {
            return ApiError("Tenant context required.", StatusCodes.Status400BadRequest);
        }

        var query = _context.Branches
            .Include(b => b.Organization)
            .AsQueryable();

        if (targetOrgId != Guid.Empty)
        {
            query = query.Where(b => b.OrganizationId == targetOrgId);
        }

        // Branch administrator (Principal) can only list their own assigned branch
        if (!CurrentUserService.IsSuperAdmin && CurrentUserService.RoleType == UserRoleType.Principal && CurrentUserService.BranchId.HasValue)
        {
            query = query.Where(b => b.Id == CurrentUserService.BranchId.Value);
        }

        if (isActive.HasValue)
        {
            query = query.Where(b => b.IsActive == isActive.Value);
        }

        var branches = await query
            .OrderBy(b => b.IsMainBranch ? 0 : 1)
            .ThenBy(b => b.Name)
            .Select(b => new BranchDto(
                b.Id,
                b.OrganizationId,
                b.Organization != null ? b.Organization.Name : null,
                b.Name,
                b.Code,
                b.Address,
                b.Phone,
                b.Email,
                b.IsMainBranch,
                b.IsActive,
                b.CreatedAtUtc
            ))
            .ToListAsync(cancellationToken);

        return ApiSuccess(branches);
    }

    [HttpGet("{id:guid}")]
    [RequirePermission(Permissions.BranchesView)]
    public async Task<IActionResult> GetBranchById(Guid id, CancellationToken cancellationToken)
    {
        var branch = await _context.Branches
            .Include(b => b.Organization)
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);

        if (branch == null)
            return ApiError("Branch not found.", StatusCodes.Status404NotFound);

        if (!CurrentUserService.IsSuperAdmin)
        {
            if (branch.OrganizationId != CurrentUserService.OrganizationId)
            {
                return ApiError("Unauthorized access to branch outside your organization.", StatusCodes.Status403Forbidden);
            }

            if (CurrentUserService.RoleType == UserRoleType.Principal && CurrentUserService.BranchId.HasValue && branch.Id != CurrentUserService.BranchId.Value)
            {
                return ApiError("Unauthorized access to another branch.", StatusCodes.Status403Forbidden);
            }
        }

        var dto = new BranchDto(
            branch.Id,
            branch.OrganizationId,
            branch.Organization?.Name,
            branch.Name,
            branch.Code,
            branch.Address,
            branch.Phone,
            branch.Email,
            branch.IsMainBranch,
            branch.IsActive,
            branch.CreatedAtUtc
        );

        return ApiSuccess(dto);
    }

    [HttpGet("{id:guid}/dashboard")]
    [HttpGet("{id:guid}/stats")]
    [RequirePermission(Permissions.BranchesView)]
    public async Task<IActionResult> GetBranchDashboard(Guid id, CancellationToken cancellationToken)
    {
        var branch = await _context.Branches
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);

        if (branch == null)
            return ApiError("Branch not found.", StatusCodes.Status404NotFound);

        if (!CurrentUserService.IsSuperAdmin)
        {
            if (branch.OrganizationId != CurrentUserService.OrganizationId)
                return ApiError("Unauthorized access to branch outside your organization.", StatusCodes.Status403Forbidden);

            if (CurrentUserService.RoleType == UserRoleType.Principal && CurrentUserService.BranchId.HasValue && branch.Id != CurrentUserService.BranchId.Value)
                return ApiError("Unauthorized access to another branch.", StatusCodes.Status403Forbidden);
        }

        if (!branch.IsActive && !CurrentUserService.IsSuperAdmin && CurrentUserService.RoleType != UserRoleType.OrganizationAdmin)
        {
            return ApiError("This branch has been deactivated.", StatusCodes.Status403Forbidden);
        }

        int totalStudents = await _context.Students
            .CountAsync(s => s.BranchId == id && !s.IsDeleted, cancellationToken);

        int totalStaff = await _context.StaffProfiles
            .CountAsync(sp => sp.BranchId == id && !sp.IsDeleted, cancellationToken);

        int totalTeachers = await _context.StaffProfiles
            .CountAsync(sp => sp.BranchId == id && !sp.IsDeleted && (sp.Designation.Contains("Teach") || sp.Department.Contains("Academic")), cancellationToken);

        int totalClasses = await _context.ClassGrades
            .CountAsync(cg => cg.BranchId == id && !cg.IsDeleted, cancellationToken);

        int totalSections = await _context.ClassSections
            .CountAsync(cs => cs.BranchId == id && !cs.IsDeleted, cancellationToken);

        var today = DateTime.UtcNow.Date;
        var attRecords = await _context.AttendanceRecords
            .Where(a => a.BranchId == id && a.Date >= today && a.Date < today.AddDays(1) && !a.IsDeleted)
            .ToListAsync(cancellationToken);

        double todayAttPct = attRecords.Count > 0
            ? Math.Round(((double)attRecords.Count(a => a.Status == AttendanceStatus.Present || a.Status == AttendanceStatus.Late) / attRecords.Count) * 100, 1)
            : 100.0;

        var startOfMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        decimal totalFeesCollected = await _context.FeePayments
            .Where(fp => fp.BranchId == id && fp.PaymentDate >= startOfMonth && !fp.IsDeleted)
            .SumAsync(fp => fp.AmountPaid, cancellationToken);

        decimal totalFeesPending = await _context.FeeInvoices
            .Where(fi => fi.BranchId == id && !fi.IsDeleted && fi.Status != FeeStatus.Paid)
            .SumAsync(fi => fi.TotalAmount - fi.PaidAmount, cancellationToken);

        var dashboard = new BranchDashboardDto(
            branch.Id,
            branch.OrganizationId,
            branch.Name,
            branch.Code,
            branch.IsActive,
            branch.IsMainBranch,
            branch.Address,
            branch.Phone,
            branch.Email,
            totalStudents,
            totalTeachers,
            totalStaff,
            totalClasses,
            totalSections,
            todayAttPct,
            totalFeesCollected,
            totalFeesPending
        );

        return ApiSuccess(dashboard);
    }

    [HttpGet("{id:guid}/users")]
    [RequirePermission(Permissions.UsersView)]
    public async Task<IActionResult> GetBranchUsers(Guid id, CancellationToken cancellationToken)
    {
        var branch = await _context.Branches
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);

        if (branch == null)
            return ApiError("Branch not found.", StatusCodes.Status404NotFound);

        if (!CurrentUserService.IsSuperAdmin)
        {
            if (branch.OrganizationId != CurrentUserService.OrganizationId)
                return ApiError("Unauthorized access to branch outside your organization.", StatusCodes.Status403Forbidden);

            if (CurrentUserService.RoleType == UserRoleType.Principal && CurrentUserService.BranchId.HasValue && branch.Id != CurrentUserService.BranchId.Value)
                return ApiError("Unauthorized access to another branch.", StatusCodes.Status403Forbidden);
        }

        var users = await _context.Users
            .Where(u => u.BranchId == id && !u.IsDeleted)
            .OrderBy(u => u.LastName).ThenBy(u => u.FirstName)
            .Select(u => new BranchUserDto(
                u.Id,
                u.FullName,
                u.Email,
                u.PhoneNumber,
                u.RoleType.ToString(),
                u.IsActive,
                u.CreatedAtUtc
            ))
            .ToListAsync(cancellationToken);

        return ApiSuccess(users);
    }

    [HttpPost]
    [RequirePermission(Permissions.BranchesManage)]
    public async Task<IActionResult> CreateBranch([FromBody] CreateBranchRequestDto request, CancellationToken cancellationToken)
    {
        if (CurrentUserService.RoleType == UserRoleType.Principal)
            return ApiError("Branch Admins are not authorized to create new branches. Only Organization Administrators can allocate new branches.", StatusCodes.Status403Forbidden);

        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Code))
            return ApiError("Branch name and code are required.");

        Guid targetOrgId = CurrentUserService.IsSuperAdmin && request.OrganizationId != Guid.Empty
            ? request.OrganizationId
            : (CurrentUserService.OrganizationId ?? Guid.Empty);

        if (targetOrgId == Guid.Empty)
            return ApiError("Valid organization ID is required.");

        if (!CurrentUserService.IsSuperAdmin && targetOrgId != CurrentUserService.OrganizationId)
            return ApiError("Unauthorized to create branch outside your organization.", StatusCodes.Status403Forbidden);

        // Enforce SuperAdmin-allocated MaxBranches capacity
        var org = await _context.Organizations.FirstOrDefaultAsync(o => o.Id == targetOrgId, cancellationToken);
        if (org != null)
        {
            int currentActiveBranches = await _context.Branches
                .CountAsync(b => b.OrganizationId == targetOrgId && b.IsActive && !b.IsDeleted, cancellationToken);
            if (currentActiveBranches >= org.MaxBranches)
            {
                return ApiError($"Branch limit reached. Your organization is granted a maximum of {org.MaxBranches} branch(es). Please contact SuperAdmin to upgrade your branch capacity or deactivate an unused branch.", StatusCodes.Status400BadRequest);
            }
        }

        string normCode = request.Code.Trim().ToUpperInvariant();
        bool codeExists = await _context.Branches.AnyAsync(b =>
            b.OrganizationId == targetOrgId &&
            b.Code.ToUpper() == normCode,
            cancellationToken);

        if (codeExists)
            return ApiError($"A branch with code '{request.Code}' already exists in this organization.");

        // If this branch is set as main branch, demote previous main branch
        if (request.IsMainBranch)
        {
            var existingMain = await _context.Branches
                .Where(b => b.OrganizationId == targetOrgId && b.IsMainBranch)
                .ToListAsync(cancellationToken);

            foreach (var b in existingMain)
            {
                b.IsMainBranch = false;
            }
        }

        var branch = new Branch
        {
            Id = Guid.NewGuid(),
            OrganizationId = targetOrgId,
            Name = request.Name.Trim(),
            Code = normCode,
            Address = request.Address?.Trim() ?? string.Empty,
            Phone = request.Phone?.Trim() ?? string.Empty,
            Email = request.Email?.Trim() ?? string.Empty,
            IsMainBranch = request.IsMainBranch,
            IsActive = true
        };

        _context.Branches.Add(branch);
        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            AuditAction.Create, "Branches", "Branch", branch.Id.ToString(),
            $"Created branch '{branch.Name}' ({branch.Code}) in organization",
            organizationId: targetOrgId,
            branchId: branch.Id,
            userId: CurrentUserService.UserId,
            userEmail: CurrentUserService.Email,
            cancellationToken: cancellationToken);

        return ApiSuccess(new { id = branch.Id, code = branch.Code }, "Branch created successfully.");
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(Permissions.BranchesManage)]
    public async Task<IActionResult> UpdateBranch(Guid id, [FromBody] UpdateBranchRequestDto request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return ApiError("Branch name is required.");

        var branch = await _context.Branches.FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
        if (branch == null)
            return ApiError("Branch not found.", StatusCodes.Status404NotFound);

        if (!CurrentUserService.IsSuperAdmin)
        {
            if (branch.OrganizationId != CurrentUserService.OrganizationId)
                return ApiError("Unauthorized access to branch outside your organization.", StatusCodes.Status403Forbidden);

            if (CurrentUserService.RoleType == UserRoleType.Principal)
            {
                if (!CurrentUserService.BranchId.HasValue || branch.Id != CurrentUserService.BranchId.Value)
                    return ApiError("Unauthorized access to another branch.", StatusCodes.Status403Forbidden);

                if (request.IsMainBranch != branch.IsMainBranch)
                    return ApiError("Branch Admins are not authorized to modify the primary campus designation.", StatusCodes.Status403Forbidden);
            }
        }

        // If toggling on MainBranch, demote previous main branches
        if (request.IsMainBranch && !branch.IsMainBranch)
        {
            var existingMain = await _context.Branches
                .Where(b => b.OrganizationId == branch.OrganizationId && b.Id != id && b.IsMainBranch)
                .ToListAsync(cancellationToken);

            foreach (var b in existingMain)
            {
                b.IsMainBranch = false;
            }
        }

        branch.Name = request.Name.Trim();
        branch.Address = request.Address?.Trim() ?? string.Empty;
        branch.Phone = request.Phone?.Trim() ?? string.Empty;
        branch.Email = request.Email?.Trim() ?? string.Empty;
        branch.IsMainBranch = request.IsMainBranch;

        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            AuditAction.Update, "Branches", "Branch", branch.Id.ToString(),
            $"Updated branch '{branch.Name}' details",
            organizationId: branch.OrganizationId,
            branchId: branch.Id,
            userId: CurrentUserService.UserId,
            userEmail: CurrentUserService.Email,
            cancellationToken: cancellationToken);

        return ApiSuccess("Branch updated successfully.");
    }

    [HttpGet("{id:guid}/settings")]
    [RequirePermission(Permissions.BranchesView)]
    public async Task<IActionResult> GetBranchSettings(Guid id, CancellationToken cancellationToken)
    {
        var branch = await _context.Branches.FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
        if (branch == null)
            return ApiError("Branch not found.", StatusCodes.Status404NotFound);

        if (!CurrentUserService.IsSuperAdmin)
        {
            if (branch.OrganizationId != CurrentUserService.OrganizationId)
                return ApiError("Unauthorized access to branch outside your organization.", StatusCodes.Status403Forbidden);

            if (CurrentUserService.RoleType == UserRoleType.Principal && CurrentUserService.BranchId.HasValue && branch.Id != CurrentUserService.BranchId.Value)
                return ApiError("Unauthorized access to another branch.", StatusCodes.Status403Forbidden);
        }

        var settings = new
        {
            branchId = branch.Id,
            branchName = branch.Name,
            branchCode = branch.Code,
            address = branch.Address,
            phone = branch.Phone,
            email = branch.Email,
            isMainBranch = branch.IsMainBranch,
            isActive = branch.IsActive
        };

        return ApiSuccess(settings);
    }

    [HttpPut("{id:guid}/settings")]
    [RequirePermission(Permissions.BranchesManage)]
    public async Task<IActionResult> UpdateBranchSettings(Guid id, [FromBody] UpdateBranchSettingsRequestDto request, CancellationToken cancellationToken)
    {
        var branch = await _context.Branches.FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
        if (branch == null)
            return ApiError("Branch not found.", StatusCodes.Status404NotFound);

        if (!CurrentUserService.IsSuperAdmin)
        {
            if (branch.OrganizationId != CurrentUserService.OrganizationId)
                return ApiError("Unauthorized access to branch outside your organization.", StatusCodes.Status403Forbidden);

            if (CurrentUserService.RoleType == UserRoleType.Principal && CurrentUserService.BranchId.HasValue && branch.Id != CurrentUserService.BranchId.Value)
                return ApiError("Unauthorized access to another branch.", StatusCodes.Status403Forbidden);
        }

        if (!string.IsNullOrWhiteSpace(request.ContactPhone))
            branch.Phone = request.ContactPhone.Trim();

        if (!string.IsNullOrWhiteSpace(request.ContactEmail))
            branch.Email = request.ContactEmail.Trim();

        if (!string.IsNullOrWhiteSpace(request.Notes) && string.IsNullOrWhiteSpace(branch.Address))
            branch.Address = request.Notes.Trim();

        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            AuditAction.Update, "Branches", "BranchSettings", branch.Id.ToString(),
            $"Updated settings for branch '{branch.Name}'",
            organizationId: branch.OrganizationId,
            branchId: branch.Id,
            userId: CurrentUserService.UserId,
            userEmail: CurrentUserService.Email,
            cancellationToken: cancellationToken);

        return ApiSuccess("Branch settings updated successfully.");
    }

    [HttpPatch("{id:guid}/toggle-status")]
    [HttpPut("{id:guid}/toggle-status")]
    [HttpPut("{id:guid}/status")]
    [RequirePermission(Permissions.BranchesManage)]
    public async Task<IActionResult> ToggleStatus(Guid id, CancellationToken cancellationToken)
    {
        if (CurrentUserService.RoleType == UserRoleType.Principal)
            return ApiError("Branch Admins are not authorized to activate or deactivate campus branches.", StatusCodes.Status403Forbidden);

        var branch = await _context.Branches.FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
        if (branch == null)
            return ApiError("Branch not found.", StatusCodes.Status404NotFound);

        if (!CurrentUserService.IsSuperAdmin && branch.OrganizationId != CurrentUserService.OrganizationId)
            return ApiError("Unauthorized access to branch outside your organization.", StatusCodes.Status403Forbidden);

        if (branch.IsMainBranch && branch.IsActive)
        {
            int otherActiveBranches = await _context.Branches
                .CountAsync(b => b.OrganizationId == branch.OrganizationId && b.Id != id && b.IsActive && !b.IsDeleted, cancellationToken);

            if (otherActiveBranches == 0)
                return ApiError("Cannot deactivate the only active campus branch of the school.", StatusCodes.Status400BadRequest);
        }

        if (!branch.IsActive)
        {
            var org = await _context.Organizations.FirstOrDefaultAsync(o => o.Id == branch.OrganizationId, cancellationToken);
            if (org != null)
            {
                int currentActiveBranches = await _context.Branches
                    .CountAsync(b => b.OrganizationId == branch.OrganizationId && b.IsActive && !b.IsDeleted, cancellationToken);
                if (currentActiveBranches >= org.MaxBranches)
                {
                    return ApiError($"Cannot activate branch. Your organization has reached its maximum limit of {org.MaxBranches} active branch(es).", StatusCodes.Status400BadRequest);
                }
            }
        }

        branch.IsActive = !branch.IsActive;
        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            AuditAction.Update, "Branches", "Branch", branch.Id.ToString(),
            $"Branch '{branch.Name}' status toggled to Active={branch.IsActive}",
            organizationId: branch.OrganizationId,
            branchId: branch.Id,
            userId: CurrentUserService.UserId,
            userEmail: CurrentUserService.Email,
            cancellationToken: cancellationToken);

        return ApiSuccess(new { id = branch.Id, isActive = branch.IsActive },
            $"Branch status changed to {(branch.IsActive ? "Active" : "Inactive")}.");
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(Permissions.BranchesManage)]
    public async Task<IActionResult> DeleteBranch(Guid id, CancellationToken cancellationToken)
    {
        if (CurrentUserService.RoleType == UserRoleType.Principal)
            return ApiError("Branch Admins are not authorized to delete campus branches.", StatusCodes.Status403Forbidden);

        var branch = await _context.Branches.FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
        if (branch == null)
            return ApiError("Branch not found.", StatusCodes.Status404NotFound);

        if (!CurrentUserService.IsSuperAdmin && branch.OrganizationId != CurrentUserService.OrganizationId)
            return ApiError("Unauthorized access to branch outside your organization.", StatusCodes.Status403Forbidden);

        if (branch.IsMainBranch)
            return ApiError("Cannot delete the primary main campus branch of an organization.", StatusCodes.Status400BadRequest);

        bool hasUsers = await _context.Users.AnyAsync(u => u.BranchId == id && !u.IsDeleted, cancellationToken);
        if (hasUsers)
            return ApiError("Cannot delete branch because active users are currently assigned to it.", StatusCodes.Status400BadRequest);

        branch.IsDeleted = true;
        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            AuditAction.Delete, "Branches", "Branch", branch.Id.ToString(),
            $"Soft-deleted branch '{branch.Name}' ({branch.Code})",
            organizationId: branch.OrganizationId,
            branchId: branch.Id,
            userId: CurrentUserService.UserId,
            userEmail: CurrentUserService.Email,
            cancellationToken: cancellationToken);

        return ApiSuccess("Branch deleted successfully.");
    }
}
