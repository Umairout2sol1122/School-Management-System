using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Api.Authorization;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.Common.Models;
using SchoolManagement.Domain.Constants;
using SchoolManagement.Domain.Entities.Academics;
using SchoolManagement.Domain.Entities.Security;
using SchoolManagement.Domain.Entities.Staff;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class StaffController : BaseApiController
{
    private readonly IApplicationDbContext _context;
    private readonly IAuditService _auditService;
    private readonly IPasswordHasher _passwordHasher;

    public StaffController(
        IApplicationDbContext context,
        IAuditService auditService,
        IPasswordHasher passwordHasher)
    {
        _context = context;
        _auditService = auditService;
        _passwordHasher = passwordHasher;
    }

    [HttpGet]
    [RequirePermission(Permissions.TeachersView)]
    public async Task<IActionResult> GetStaff(
        [FromQuery] string? search,
        [FromQuery] string? department,
        [FromQuery] string? designation,
        [FromQuery] EmploymentType? employmentType,
        [FromQuery] StaffStatus? status,
        [FromQuery] Guid? branchId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 100);

        Guid targetOrgId = CurrentUserService.IsSuperAdmin ? (TenantContext.OrganizationId ?? Guid.Empty) : (CurrentUserService.OrganizationId ?? Guid.Empty);

        var query = _context.StaffProfiles
            .Include(sp => sp.Branch)
            .Include(sp => sp.User)
            .AsQueryable();

        if (targetOrgId != Guid.Empty)
        {
            query = query.Where(sp => sp.OrganizationId == targetOrgId);
        }

        // Branch Isolation: Principal can only view staff in their own branch
        if (!CurrentUserService.IsSuperAdmin && CurrentUserService.BranchId.HasValue && CurrentUserService.RoleType == UserRoleType.Principal)
        {
            query = query.Where(sp => sp.BranchId == CurrentUserService.BranchId.Value);
        }
        else if (branchId.HasValue)
        {
            query = query.Where(sp => sp.BranchId == branchId.Value);
        }

        if (!string.IsNullOrWhiteSpace(department))
        {
            query = query.Where(sp => sp.Department.ToLower() == department.Trim().ToLower());
        }

        if (!string.IsNullOrWhiteSpace(designation))
        {
            query = query.Where(sp => sp.Designation.ToLower() == designation.Trim().ToLower());
        }

        if (employmentType.HasValue)
        {
            query = query.Where(sp => sp.EmploymentType == employmentType.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(sp => sp.Status == status.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            string term = search.Trim().ToLower();
            query = query.Where(sp =>
                sp.EmployeeCode.ToLower().Contains(term) ||
                (sp.User != null && (
                    sp.User.FirstName.ToLower().Contains(term) ||
                    sp.User.LastName.ToLower().Contains(term) ||
                    sp.User.Email.ToLower().Contains(term) ||
                    (sp.User.PhoneNumber != null && sp.User.PhoneNumber.ToLower().Contains(term))
                )));
        }

        int totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(sp => sp.EmployeeCode)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(sp => new StaffSummaryDto(
                sp.Id,
                sp.UserId,
                sp.OrganizationId,
                sp.BranchId,
                sp.Branch != null ? sp.Branch.Name : null,
                sp.EmployeeCode,
                sp.User != null ? sp.User.FullName : sp.EmployeeCode,
                sp.User != null ? sp.User.Email : string.Empty,
                sp.User != null ? sp.User.PhoneNumber : null,
                sp.Designation,
                sp.Department,
                sp.EmploymentType,
                sp.Status,
                sp.JoiningDate
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

    [HttpGet("{id:guid}")]
    [RequirePermission(Permissions.TeachersView)]
    public async Task<IActionResult> GetStaffById(Guid id, CancellationToken cancellationToken)
    {
        var staff = await _context.StaffProfiles
            .Include(sp => sp.Branch)
            .Include(sp => sp.User)
                .ThenInclude(u => u!.UserRoles)
                    .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(sp => sp.Id == id, cancellationToken);

        if (staff == null)
            return ApiError("Staff member not found.", StatusCodes.Status404NotFound);

        if (!CurrentUserService.IsSuperAdmin && staff.OrganizationId != CurrentUserService.OrganizationId)
            return ApiError("Unauthorized access to staff member outside your organization.", StatusCodes.Status403Forbidden);

        if (!CurrentUserService.IsSuperAdmin && CurrentUserService.BranchId.HasValue && CurrentUserService.RoleType == UserRoleType.Principal)
        {
            if (staff.BranchId != CurrentUserService.BranchId.Value)
                return ApiForbidden("You do not have access to staff members outside your assigned branch.");
        }

        var roles = staff.User?.UserRoles
            .Where(ur => ur.Role != null)
            .Select(ur => ur.Role!.Name)
            .ToList() ?? new List<string>();

        var details = new StaffDetailsDto(
            staff.Id,
            staff.UserId,
            staff.OrganizationId,
            staff.BranchId,
            staff.Branch?.Name,
            staff.EmployeeCode,
            staff.User?.FirstName ?? string.Empty,
            staff.User?.LastName ?? string.Empty,
            staff.User?.FullName ?? staff.EmployeeCode,
            staff.User?.Email ?? string.Empty,
            staff.User?.PhoneNumber,
            staff.Designation,
            staff.Department,
            staff.Qualification,
            staff.Specialization,
            staff.EmploymentType,
            staff.Status,
            staff.JoiningDate,
            staff.BasicSalary,
            staff.EmergencyContactName,
            staff.EmergencyContactPhone,
            staff.NationalId,
            roles,
            staff.CreatedAtUtc
        );

        return ApiSuccess(details);
    }

    [HttpPost]
    [RequirePermission(Permissions.TeachersCreate)]
    public async Task<IActionResult> CreateStaff([FromBody] CreateStaffRequestDto request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.LastName))
            return ApiError("Staff first name and last name are required.");

        if (string.IsNullOrWhiteSpace(request.Email))
            return ApiError("Email address is required.");

        if (string.IsNullOrWhiteSpace(request.Designation))
            return ApiError("Designation is required.");

        if (string.IsNullOrWhiteSpace(request.Department))
            return ApiError("Department is required.");

        Guid targetOrgId = CurrentUserService.IsSuperAdmin && TenantContext.OrganizationId.HasValue
            ? TenantContext.OrganizationId.Value
            : (CurrentUserService.OrganizationId ?? Guid.Empty);

        if (targetOrgId == Guid.Empty)
            return ApiError("Tenant context required.", StatusCodes.Status400BadRequest);

        Guid? targetBranchId = request.BranchId ?? CurrentUserService.BranchId;
        if (!CurrentUserService.IsSuperAdmin && CurrentUserService.BranchId.HasValue && CurrentUserService.RoleType == UserRoleType.Principal)
        {
            targetBranchId = CurrentUserService.BranchId.Value;
        }

        if (targetBranchId.HasValue)
        {
            bool branchExists = await _context.Branches.AnyAsync(b =>
                b.Id == targetBranchId.Value &&
                b.OrganizationId == targetOrgId &&
                !b.IsDeleted, cancellationToken);

            if (!branchExists)
                return ApiError("Specified branch does not exist or does not belong to this organization.");
        }

        // Email uniqueness check (including soft-deleted accounts)
        string normalizedEmail = request.Email.Trim().ToUpperInvariant();
        bool emailExists = await _context.Users
            .IgnoreQueryFilters()
            .AnyAsync(u =>
                u.OrganizationId == targetOrgId &&
                u.NormalizedEmail == normalizedEmail,
                cancellationToken);

        if (emailExists)
            return ApiError("A user with this email address already exists in this organization.");

        // Generate EmployeeCode if empty
        string employeeCode = request.EmployeeCode?.Trim().ToUpperInvariant() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(employeeCode))
        {
            int staffCount = await _context.StaffProfiles
                .IgnoreQueryFilters()
                .CountAsync(sp => sp.OrganizationId == targetOrgId, cancellationToken) + 1;
            employeeCode = $"EMP-{DateTime.UtcNow.Year}-{staffCount:D4}";

            while (await _context.StaffProfiles.IgnoreQueryFilters().AnyAsync(sp => sp.OrganizationId == targetOrgId && sp.EmployeeCode == employeeCode, cancellationToken))
            {
                staffCount++;
                employeeCode = $"EMP-{DateTime.UtcNow.Year}-{staffCount:D4}";
            }
        }

        bool codeExists = await _context.StaffProfiles
            .IgnoreQueryFilters()
            .AnyAsync(sp =>
                sp.OrganizationId == targetOrgId &&
                sp.EmployeeCode.ToUpper() == employeeCode,
                cancellationToken);

        if (codeExists)
            return ApiError($"A staff member with employee code '{employeeCode}' already exists in this organization.");

        // Password setup
        string password = string.IsNullOrWhiteSpace(request.InitialPassword) ? "Staff@2026!Pak" : request.InitialPassword;
        var (hash, salt) = _passwordHasher.HashPassword(password);

        bool isTeacher = request.Designation.ToLowerInvariant().Contains("teach") ||
                         request.Department.ToLowerInvariant().Contains("teach") ||
                         request.Department.ToLowerInvariant().Contains("academic");

        var userRoleType = isTeacher ? UserRoleType.Teacher : UserRoleType.Staff;

        var user = new User
        {
            Id = Guid.NewGuid(),
            OrganizationId = targetOrgId,
            BranchId = targetBranchId,
            Username = request.Email.Trim(),
            Email = request.Email.Trim(),
            NormalizedEmail = normalizedEmail,
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            PasswordHash = hash,
            PasswordSalt = salt,
            PhoneNumber = request.PhoneNumber?.Trim(),
            RoleType = userRoleType,
            IsActive = true
        };

        // Assign Roles
        if (request.RoleIds != null && request.RoleIds.Count > 0)
        {
            var validRoleIds = await _context.Roles
                .Where(r => request.RoleIds.Contains(r.Id) && (r.IsSystemRole || r.OrganizationId == targetOrgId))
                .Select(r => r.Id)
                .ToListAsync(cancellationToken);

            foreach (var rId in validRoleIds)
            {
                user.UserRoles.Add(new UserRole
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    RoleId = rId
                });
            }
        }
        else
        {
            // Default system role
            string defaultRoleName = isTeacher ? nameof(UserRoleType.Teacher) : nameof(UserRoleType.Staff);
            var systemRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == defaultRoleName && r.IsSystemRole, cancellationToken);
            if (systemRole != null)
            {
                user.UserRoles.Add(new UserRole
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    RoleId = systemRole.Id
                });
            }
        }

        var staffProfile = new StaffProfile
        {
            Id = Guid.NewGuid(),
            OrganizationId = targetOrgId,
            BranchId = targetBranchId,
            UserId = user.Id,
            EmployeeCode = employeeCode,
            Designation = request.Designation.Trim(),
            Department = request.Department.Trim(),
            Qualification = request.Qualification?.Trim(),
            Specialization = request.Specialization?.Trim(),
            EmploymentType = request.EmploymentType,
            Status = StaffStatus.Active,
            JoiningDate = request.JoiningDate ?? DateTime.UtcNow,
            BasicSalary = request.BasicSalary,
            EmergencyContactName = request.EmergencyContactName?.Trim(),
            EmergencyContactPhone = request.EmergencyContactPhone?.Trim(),
            NationalId = request.NationalId?.Trim()
        };

        _context.Users.Add(user);
        _context.StaffProfiles.Add(staffProfile);
        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            AuditAction.Create, "Staff", "StaffProfile", staffProfile.Id.ToString(),
            $"Onboarded staff member '{user.FullName}' ({staffProfile.EmployeeCode}) as '{staffProfile.Designation}' in '{staffProfile.Department}'",
            organizationId: targetOrgId,
            branchId: targetBranchId,
            userId: CurrentUserService.UserId,
            userEmail: CurrentUserService.Email,
            cancellationToken: cancellationToken);

        return ApiSuccess(new
        {
            id = staffProfile.Id,
            userId = user.Id,
            employeeCode = staffProfile.EmployeeCode,
            fullName = user.FullName,
            email = user.Email,
            designation = staffProfile.Designation
        }, "Staff member onboarded successfully.");
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(Permissions.TeachersEdit)]
    public async Task<IActionResult> UpdateStaff(Guid id, [FromBody] UpdateStaffRequestDto request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.LastName))
            return ApiError("Staff first name and last name are required.");

        if (string.IsNullOrWhiteSpace(request.Designation))
            return ApiError("Designation is required.");

        if (string.IsNullOrWhiteSpace(request.Department))
            return ApiError("Department is required.");

        var staff = await _context.StaffProfiles
            .Include(sp => sp.User)
            .FirstOrDefaultAsync(sp => sp.Id == id, cancellationToken);

        if (staff == null)
            return ApiError("Staff member not found.", StatusCodes.Status404NotFound);

        if (!CurrentUserService.IsSuperAdmin && staff.OrganizationId != CurrentUserService.OrganizationId)
            return ApiError("Unauthorized access to staff member outside your organization.", StatusCodes.Status403Forbidden);

        if (!CurrentUserService.IsSuperAdmin && CurrentUserService.BranchId.HasValue && CurrentUserService.RoleType == UserRoleType.Principal)
        {
            if (staff.BranchId != CurrentUserService.BranchId.Value)
                return ApiForbidden("You cannot update staff members outside your assigned branch.");
        }

        if (request.BranchId.HasValue)
        {
            bool branchExists = await _context.Branches.AnyAsync(b =>
                b.Id == request.BranchId.Value &&
                b.OrganizationId == staff.OrganizationId &&
                !b.IsDeleted, cancellationToken);

            if (!branchExists)
                return ApiError("Specified branch does not exist or does not belong to this organization.");

            staff.BranchId = request.BranchId;
            if (staff.User != null)
                staff.User.BranchId = request.BranchId;
        }

        if (staff.User != null)
        {
            staff.User.FirstName = request.FirstName.Trim();
            staff.User.LastName = request.LastName.Trim();
            staff.User.PhoneNumber = request.PhoneNumber?.Trim();
        }

        staff.Designation = request.Designation.Trim();
        staff.Department = request.Department.Trim();
        staff.Qualification = request.Qualification?.Trim();
        staff.Specialization = request.Specialization?.Trim();
        staff.EmploymentType = request.EmploymentType;
        staff.Status = request.Status;
        staff.BasicSalary = request.BasicSalary;
        staff.EmergencyContactName = request.EmergencyContactName?.Trim();
        staff.EmergencyContactPhone = request.EmergencyContactPhone?.Trim();
        staff.NationalId = request.NationalId?.Trim();

        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            AuditAction.Update, "Staff", "StaffProfile", staff.Id.ToString(),
            $"Updated staff profile for '{staff.User?.FullName ?? staff.EmployeeCode}' ({staff.EmployeeCode})",
            organizationId: staff.OrganizationId,
            branchId: staff.BranchId,
            userId: CurrentUserService.UserId,
            userEmail: CurrentUserService.Email,
            cancellationToken: cancellationToken);

        return ApiSuccess("Staff profile updated successfully.");
    }

    [HttpPatch("{id:guid}/toggle-status")]
    [RequirePermission(Permissions.TeachersEdit)]
    public async Task<IActionResult> ToggleStaffStatus(Guid id, [FromQuery] StaffStatus newStatus, CancellationToken cancellationToken)
    {
        var staff = await _context.StaffProfiles
            .Include(sp => sp.User)
            .FirstOrDefaultAsync(sp => sp.Id == id, cancellationToken);

        if (staff == null)
            return ApiError("Staff member not found.", StatusCodes.Status404NotFound);

        if (!CurrentUserService.IsSuperAdmin && staff.OrganizationId != CurrentUserService.OrganizationId)
            return ApiError("Unauthorized access to staff member outside your organization.", StatusCodes.Status403Forbidden);

        if (!CurrentUserService.IsSuperAdmin && CurrentUserService.BranchId.HasValue && CurrentUserService.RoleType == UserRoleType.Principal)
        {
            if (staff.BranchId != CurrentUserService.BranchId.Value)
                return ApiForbidden("You cannot modify staff members outside your assigned branch.");
        }

        staff.Status = newStatus;
        if (staff.User != null)
        {
            // If terminated or resigned, disable user account login
            staff.User.IsActive = (newStatus == StaffStatus.Active || newStatus == StaffStatus.OnLeave);
        }

        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            AuditAction.Update, "Staff", "StaffProfile", staff.Id.ToString(),
            $"Changed staff '{staff.EmployeeCode}' status to '{newStatus}'",
            organizationId: staff.OrganizationId,
            branchId: staff.BranchId,
            userId: CurrentUserService.UserId,
            userEmail: CurrentUserService.Email,
            cancellationToken: cancellationToken);

        return ApiSuccess(new { status = staff.Status, isUserActive = staff.User?.IsActive ?? false }, "Staff status updated successfully.");
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(Permissions.TeachersDelete)]
    public async Task<IActionResult> DeleteStaff(Guid id, CancellationToken cancellationToken)
    {
        var staff = await _context.StaffProfiles
            .Include(sp => sp.User)
            .FirstOrDefaultAsync(sp => sp.Id == id, cancellationToken);

        if (staff == null)
            return ApiError("Staff member not found.", StatusCodes.Status404NotFound);

        if (!CurrentUserService.IsSuperAdmin && staff.OrganizationId != CurrentUserService.OrganizationId)
            return ApiError("Unauthorized access to staff member outside your organization.", StatusCodes.Status403Forbidden);

        if (!CurrentUserService.IsSuperAdmin && CurrentUserService.BranchId.HasValue && CurrentUserService.RoleType == UserRoleType.Principal)
        {
            if (staff.BranchId != CurrentUserService.BranchId.Value)
                return ApiForbidden("You cannot delete staff members outside your assigned branch.");
        }

        staff.IsDeleted = true;
        if (staff.User != null)
        {
            staff.User.IsActive = false;
            staff.User.IsDeleted = true;
        }

        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            AuditAction.Delete, "Staff", "StaffProfile", staff.Id.ToString(),
            $"Soft-deleted staff member '{staff.EmployeeCode}'",
            organizationId: staff.OrganizationId,
            branchId: staff.BranchId,
            userId: CurrentUserService.UserId,
            userEmail: CurrentUserService.Email,
            cancellationToken: cancellationToken);

        return ApiSuccess("Staff member deleted successfully.");
    }

    [HttpPost("{id:guid}/transfer-branch")]
    [RequirePermission(Permissions.TeachersEdit)]
    public async Task<IActionResult> TransferBranch(Guid id, [FromBody] TransferStaffBranchRequestDto request, CancellationToken cancellationToken)
    {
        var staff = await _context.StaffProfiles
            .Include(sp => sp.Branch)
            .Include(sp => sp.User)
            .FirstOrDefaultAsync(sp => sp.Id == id, cancellationToken);

        if (staff == null)
            return ApiError("Staff member not found.", StatusCodes.Status404NotFound);

        if (!CurrentUserService.IsSuperAdmin && staff.OrganizationId != CurrentUserService.OrganizationId)
            return ApiError("Unauthorized access to staff member outside your organization.", StatusCodes.Status403Forbidden);

        if (!CurrentUserService.IsSuperAdmin && CurrentUserService.BranchId.HasValue && CurrentUserService.RoleType == UserRoleType.Principal)
        {
            if (staff.BranchId != CurrentUserService.BranchId.Value)
                return ApiForbidden("You cannot transfer staff members outside your assigned branch.");
        }

        var targetBranch = await _context.Branches
            .FirstOrDefaultAsync(b => b.Id == request.NewBranchId && b.OrganizationId == staff.OrganizationId && !b.IsDeleted, cancellationToken);

        if (targetBranch == null)
            return ApiError("Target branch does not exist or does not belong to this organization.");

        if (!targetBranch.IsActive)
            return ApiError("Cannot transfer staff to an inactive branch.");

        string oldBranchName = staff.Branch?.Name ?? "Unassigned";
        staff.BranchId = targetBranch.Id;
        if (staff.User != null)
        {
            staff.User.BranchId = targetBranch.Id;
        }

        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            AuditAction.Update, "Staff", "StaffProfile", staff.Id.ToString(),
            $"Transferred staff member '{staff.EmployeeCode}' from '{oldBranchName}' to '{targetBranch.Name}'. Reason: {request.Reason ?? "Administrative"}",
            organizationId: staff.OrganizationId,
            branchId: targetBranch.Id,
            userId: CurrentUserService.UserId,
            userEmail: CurrentUserService.Email,
            cancellationToken: cancellationToken);

        return ApiSuccess(new
        {
            staffId = staff.Id,
            newBranchId = targetBranch.Id,
            newBranchName = targetBranch.Name
        }, $"Staff member successfully transferred to '{targetBranch.Name}'.");
    }

    [HttpGet("{id:guid}/leaves")]
    [RequirePermission(Permissions.TeachersView)]
    public async Task<IActionResult> GetStaffLeaves(Guid id, CancellationToken cancellationToken)
    {
        var staff = await _context.StaffProfiles
            .Include(sp => sp.User)
            .FirstOrDefaultAsync(sp => sp.Id == id, cancellationToken);
        if (staff == null) return ApiNotFound("Staff member not found.");

        if (!CurrentUserService.IsSuperAdmin && staff.OrganizationId != CurrentUserService.OrganizationId)
            return ApiForbidden("Unauthorized access.");

        var leaves = await _context.LeaveRequests
            .Include(lr => lr.ApprovedByUser)
            .Where(lr => lr.StaffProfileId == id && !lr.IsDeleted)
            .OrderByDescending(lr => lr.CreatedAtUtc)
            .Select(lr => new StaffLeaveDto(
                lr.Id,
                staff.Id,
                staff.EmployeeCode,
                staff.User != null ? staff.User.FullName : staff.EmployeeCode,
                lr.LeaveType,
                lr.StartDate,
                lr.EndDate,
                (int)(lr.EndDate.Date - lr.StartDate.Date).TotalDays + 1,
                lr.Reason,
                lr.AttachmentUrl,
                lr.Status,
                lr.CreatedAtUtc,
                lr.ApprovedByUser != null ? lr.ApprovedByUser.FullName : null,
                lr.DecisionRemarks,
                lr.DecisionDateUtc
            ))
            .ToListAsync(cancellationToken);

        return ApiSuccess(leaves);
    }

    [HttpPost("{id:guid}/leaves")]
    [RequirePermission(Permissions.TeachersEdit)]
    public async Task<IActionResult> SubmitStaffLeave(Guid id, [FromBody] SubmitStaffLeaveRequestDto request, CancellationToken cancellationToken)
    {
        var staff = await _context.StaffProfiles
            .Include(sp => sp.User)
            .FirstOrDefaultAsync(sp => sp.Id == id, cancellationToken);
        if (staff == null) return ApiNotFound("Staff member not found.");

        if (!CurrentUserService.IsSuperAdmin && staff.OrganizationId != CurrentUserService.OrganizationId)
            return ApiForbidden("Unauthorized access.");

        if (request.EndDate < request.StartDate)
            return ApiError("End date cannot be earlier than start date.");

        var leave = new LeaveRequest
        {
            Id = Guid.NewGuid(),
            OrganizationId = staff.OrganizationId,
            BranchId = staff.BranchId,
            RequesterUserId = staff.UserId,
            RequesterType = RequesterType.Staff,
            StaffProfileId = staff.Id,
            LeaveType = string.IsNullOrWhiteSpace(request.LeaveType) ? "Casual" : request.LeaveType.Trim(),
            StartDate = request.StartDate.Date,
            EndDate = request.EndDate.Date,
            Reason = request.Reason.Trim(),
            AttachmentUrl = request.AttachmentUrl?.Trim(),
            Status = LeaveStatus.Pending
        };

        _context.LeaveRequests.Add(leave);
        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            AuditAction.Create, "Staff", "LeaveRequest", leave.Id.ToString(),
            $"Submitted {leave.LeaveType} leave for '{staff.EmployeeCode}' from {leave.StartDate:yyyy-MM-dd} to {leave.EndDate:yyyy-MM-dd}",
            organizationId: staff.OrganizationId,
            branchId: staff.BranchId,
            userId: CurrentUserService.UserId,
            userEmail: CurrentUserService.Email,
            cancellationToken: cancellationToken);

        return ApiSuccess(new { id = leave.Id, status = leave.Status.ToString() }, "Leave request submitted successfully.");
    }

    [HttpPut("leaves/{leaveId:guid}/review")]
    [RequirePermission(Permissions.TeachersEdit)]
    public async Task<IActionResult> ReviewStaffLeave(Guid leaveId, [FromBody] ReviewStaffLeaveRequestDto request, CancellationToken cancellationToken)
    {
        var leave = await _context.LeaveRequests
            .Include(lr => lr.StaffProfile)
            .FirstOrDefaultAsync(lr => lr.Id == leaveId && !lr.IsDeleted, cancellationToken);

        if (leave == null) return ApiNotFound("Leave request not found.");

        if (!CurrentUserService.IsSuperAdmin && leave.OrganizationId != CurrentUserService.OrganizationId)
            return ApiForbidden("Unauthorized access.");

        if (!CurrentUserService.IsSuperAdmin && CurrentUserService.BranchId.HasValue && CurrentUserService.RoleType == UserRoleType.Principal)
        {
            if (leave.BranchId != CurrentUserService.BranchId.Value)
                return ApiForbidden("You cannot review leave requests outside your assigned branch.");
        }

        leave.Status = request.Status;
        leave.DecisionRemarks = request.DecisionRemarks;
        leave.DecisionDateUtc = DateTime.UtcNow;
        leave.ApprovedByUserId = CurrentUserService.UserId;

        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            AuditAction.Update, "Staff", "LeaveRequest", leave.Id.ToString(),
            $"Reviewed leave request {leave.Id} for staff '{leave.StaffProfile?.EmployeeCode}': Status set to {request.Status}",
            organizationId: leave.OrganizationId,
            branchId: leave.BranchId,
            userId: CurrentUserService.UserId,
            userEmail: CurrentUserService.Email,
            cancellationToken: cancellationToken);

        return ApiSuccess(new { id = leave.Id, status = leave.Status.ToString() }, $"Leave request marked as {request.Status}.");
    }
}
