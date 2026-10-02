using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Api.Authorization;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.Common.Models;
using SchoolManagement.Application.Common.Security;
using SchoolManagement.Domain.Constants;
using SchoolManagement.Domain.Entities.Security;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController : BaseApiController
{
    private readonly IApplicationDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IAuditService _auditService;

    public UsersController(
        IApplicationDbContext context,
        IPasswordHasher passwordHasher,
        IAuditService auditService)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _auditService = auditService;
    }

    [HttpGet]
    [RequirePermission(Permissions.UsersView)]
    public async Task<IActionResult> GetUsers(
        [FromQuery] string? search,
        [FromQuery] UserRoleType? roleType,
        [FromQuery] Guid? branchId,
        [FromQuery] bool? isActive,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _context.Users
            .Include(u => u.Organization)
            .Include(u => u.Branch)
            .AsQueryable();

        // Tenant isolation
        if (!CurrentUserService.IsSuperAdmin)
        {
            query = query.Where(u => u.OrganizationId == CurrentUserService.OrganizationId);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            string term = search.Trim().ToLower();
            query = query.Where(u =>
                u.FirstName.ToLower().Contains(term) ||
                u.LastName.ToLower().Contains(term) ||
                u.Email.ToLower().Contains(term) ||
                u.Username.ToLower().Contains(term));
        }

        if (roleType.HasValue)
        {
            query = query.Where(u => u.RoleType == roleType.Value);
        }

        if (branchId.HasValue)
        {
            query = query.Where(u => u.BranchId == branchId.Value);
        }

        if (isActive.HasValue)
        {
            query = query.Where(u => u.IsActive == isActive.Value);
        }

        int totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(u => u.CreatedAtUtc)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new UserSummaryDto(
                u.Id,
                u.OrganizationId,
                u.Organization != null ? u.Organization.Name : null,
                u.BranchId,
                u.Branch != null ? u.Branch.Name : null,
                u.Username,
                u.Email,
                u.FullName,
                u.RoleType,
                u.IsActive,
                u.IsLocked,
                u.LastLoginAtUtc
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
    [RequirePermission(Permissions.UsersView)]
    public async Task<IActionResult> GetUserById(Guid id, CancellationToken cancellationToken)
    {
        var user = await _context.Users
            .Include(u => u.Organization)
            .Include(u => u.Branch)
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role!)
                    .ThenInclude(r => r.RolePermissions)
                        .ThenInclude(rp => rp.Permission!)
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

        if (user == null)
            return ApiError("User not found.", StatusCodes.Status404NotFound);

        // Cross-tenant verification
        if (!CurrentUserService.IsSuperAdmin && user.OrganizationId != CurrentUserService.OrganizationId)
            return ApiError("Unauthorized access to user outside your organization.", StatusCodes.Status403Forbidden);

        var roles = user.UserRoles
            .Where(ur => ur.Role != null)
            .Select(ur => new RoleDto(
                ur.Role!.Id,
                ur.Role.Name,
                ur.Role.Description,
                ur.Role.IsSystemRole,
                ur.Role.RolePermissions.Count
            ))
            .ToList();

        var permissions = user.UserRoles
            .SelectMany(ur => ur.Role?.RolePermissions.Select(rp => rp.Permission?.Code) ?? Enumerable.Empty<string?>())
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code!)
            .Distinct()
            .ToList();

        var dto = new UserDetailsDto(
            user.Id,
            user.OrganizationId,
            user.Organization?.Name,
            user.BranchId,
            user.Branch?.Name,
            user.Username,
            user.Email,
            user.FirstName,
            user.LastName,
            user.FullName,
            user.PhoneNumber,
            user.RoleType,
            user.IsActive,
            user.IsLocked,
            user.LockoutEndUtc,
            user.LastLoginAtUtc,
            roles,
            permissions
        );

        return ApiSuccess(dto);
    }

    [HttpPost]
    [RequirePermission(Permissions.UsersCreate)]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequestDto request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Username) ||
            string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.FirstName) ||
            string.IsNullOrWhiteSpace(request.LastName) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return ApiError("Username, Email, First Name, Last Name, and Password are required.");
        }

        // Security check: Only SuperAdmin can create SuperAdmin accounts
        if (request.RoleType == UserRoleType.SuperAdmin && !CurrentUserService.IsSuperAdmin)
        {
            return ApiError("Only existing SuperAdmins can create SuperAdmin accounts.", StatusCodes.Status403Forbidden);
        }

        // Tenant scoping
        Guid targetOrgId = CurrentUserService.IsSuperAdmin && request.OrganizationId != Guid.Empty
            ? request.OrganizationId
            : (CurrentUserService.OrganizationId ?? Guid.Empty);

        if (targetOrgId == Guid.Empty && request.RoleType != UserRoleType.SuperAdmin)
        {
            return ApiError("Organization ID is required for non-SuperAdmin users.");
        }

        // Validate password
        var (isValid, errorMessage) = PasswordPolicyValidator.Validate(request.Password);
        if (!isValid)
            return ApiError(errorMessage ?? "Password does not meet complexity criteria.");

        // Uniqueness checks
        string normalizedEmail = request.Email.Trim().ToUpperInvariant();
        bool emailExists = await _context.Users.AnyAsync(u =>
            u.OrganizationId == targetOrgId &&
            u.NormalizedEmail == normalizedEmail,
            cancellationToken);

        if (emailExists)
            return ApiError("A user with this email already exists in this organization.");

        bool usernameExists = await _context.Users.AnyAsync(u =>
            u.OrganizationId == targetOrgId &&
            u.Username.ToLower() == request.Username.Trim().ToLower(),
            cancellationToken);

        if (usernameExists)
            return ApiError("A user with this username already exists in this organization.");

        if (request.BranchId.HasValue)
        {
            bool branchExists = await _context.Branches.AnyAsync(b =>
                b.Id == request.BranchId.Value &&
                b.OrganizationId == targetOrgId &&
                !b.IsDeleted, cancellationToken);

            if (!branchExists)
                return ApiError("Specified branch does not exist or does not belong to this organization.");
        }

        var (hash, salt) = _passwordHasher.HashPassword(request.Password);

        var user = new User
        {
            Id = Guid.NewGuid(),
            OrganizationId = targetOrgId,
            BranchId = request.BranchId,
            Username = request.Username.Trim(),
            Email = request.Email.Trim(),
            NormalizedEmail = normalizedEmail,
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            PasswordHash = hash,
            PasswordSalt = salt,
            PhoneNumber = request.PhoneNumber?.Trim(),
            RoleType = request.RoleType,
            IsActive = true
        };

        // Assign roles
        if (request.RoleIds != null && request.RoleIds.Count > 0)
        {
            var validRoleIds = await _context.Roles
                .Where(r => request.RoleIds.Contains(r.Id) && (r.IsSystemRole || r.OrganizationId == targetOrgId))
                .Select(r => r.Id)
                .ToListAsync(cancellationToken);

            foreach (var roleId in validRoleIds)
            {
                user.UserRoles.Add(new UserRole
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    RoleId = roleId
                });
            }
        }

        // Auto-assign corresponding system role if none assigned
        if (!user.UserRoles.Any())
        {
            string expectedRoleName = user.RoleType.ToString();
            var matchingRole = await _context.Roles
                .FirstOrDefaultAsync(r => r.IsSystemRole && (r.Name == expectedRoleName || r.Name.Replace(" ", "") == expectedRoleName), cancellationToken);

            if (matchingRole != null)
            {
                user.UserRoles.Add(new UserRole
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    RoleId = matchingRole.Id
                });
            }
        }

        _context.Users.Add(user);
        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            AuditAction.Create, "Security", "User", user.Id.ToString(),
            $"Created user '{user.Email}' with role '{user.RoleType}'",
            organizationId: user.OrganizationId,
            branchId: user.BranchId,
            userId: CurrentUserService.UserId,
            userEmail: CurrentUserService.Email,
            cancellationToken: cancellationToken);

        return ApiSuccess(new { id = user.Id, email = user.Email }, "User created successfully.");
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(Permissions.UsersEdit)]
    public async Task<IActionResult> UpdateUser(Guid id, [FromBody] UpdateUserRequestDto request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.LastName))
            return ApiError("First name and last name are required.");

        var user = await _context.Users
            .Include(u => u.UserRoles)
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

        if (user == null)
            return ApiError("User not found.", StatusCodes.Status404NotFound);

        // Cross-tenant verification
        if (!CurrentUserService.IsSuperAdmin && user.OrganizationId != CurrentUserService.OrganizationId)
            return ApiError("Unauthorized access to user outside your organization.", StatusCodes.Status403Forbidden);

        // Security check: Only SuperAdmin can modify SuperAdmin or promote to SuperAdmin
        if ((user.RoleType == UserRoleType.SuperAdmin || request.RoleType == UserRoleType.SuperAdmin) && !CurrentUserService.IsSuperAdmin)
        {
            return ApiError("Only SuperAdmins can modify SuperAdmin accounts.", StatusCodes.Status403Forbidden);
        }

        if (request.BranchId.HasValue)
        {
            bool branchExists = await _context.Branches.AnyAsync(b =>
                b.Id == request.BranchId.Value &&
                b.OrganizationId == user.OrganizationId &&
                !b.IsDeleted, cancellationToken);

            if (!branchExists)
                return ApiError("Specified branch does not exist or does not belong to this organization.");
        }

        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();
        user.PhoneNumber = request.PhoneNumber?.Trim();
        user.BranchId = request.BranchId;
        user.RoleType = request.RoleType;

        // Update roles
        user.UserRoles.Clear();
        if (request.RoleIds != null && request.RoleIds.Count > 0)
        {
            var validRoleIds = await _context.Roles
                .Where(r => request.RoleIds.Contains(r.Id) && (r.IsSystemRole || r.OrganizationId == user.OrganizationId))
                .Select(r => r.Id)
                .ToListAsync(cancellationToken);

            foreach (var roleId in validRoleIds)
            {
                user.UserRoles.Add(new UserRole
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    RoleId = roleId
                });
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            AuditAction.Update, "Security", "User", user.Id.ToString(),
            $"Updated user profile and roles for '{user.Email}'",
            organizationId: user.OrganizationId,
            branchId: user.BranchId,
            userId: CurrentUserService.UserId,
            userEmail: CurrentUserService.Email,
            cancellationToken: cancellationToken);

        return ApiSuccess("User updated successfully.");
    }

    [HttpPatch("{id:guid}/toggle-status")]
    [RequirePermission(Permissions.UsersDisable)]
    public async Task<IActionResult> ToggleStatus(Guid id, CancellationToken cancellationToken)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

        if (user == null)
            return ApiError("User not found.", StatusCodes.Status404NotFound);

        if (!CurrentUserService.IsSuperAdmin && user.OrganizationId != CurrentUserService.OrganizationId)
            return ApiError("Unauthorized access to user outside your organization.", StatusCodes.Status403Forbidden);

        // User cannot deactivate their own account
        if (user.Id == CurrentUserService.UserId)
            return ApiError("You cannot deactivate your own account.", StatusCodes.Status400BadRequest);

        user.IsActive = !user.IsActive;
        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            AuditAction.Update, "Security", "User", user.Id.ToString(),
            $"User '{user.Email}' status toggled to Active={user.IsActive}",
            organizationId: user.OrganizationId,
            branchId: user.BranchId,
            userId: CurrentUserService.UserId,
            userEmail: CurrentUserService.Email,
            cancellationToken: cancellationToken);

        return ApiSuccess(new { id = user.Id, isActive = user.IsActive },
            $"User account has been {(user.IsActive ? "activated" : "deactivated")} successfully.");
    }

    [HttpPost("{id:guid}/unlock")]
    [RequirePermission(Permissions.UsersEdit)]
    public async Task<IActionResult> UnlockAccount(Guid id, CancellationToken cancellationToken)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

        if (user == null)
            return ApiError("User not found.", StatusCodes.Status404NotFound);

        if (!CurrentUserService.IsSuperAdmin && user.OrganizationId != CurrentUserService.OrganizationId)
            return ApiError("Unauthorized access to user outside your organization.", StatusCodes.Status403Forbidden);

        user.IsLocked = false;
        user.LockoutEndUtc = null;
        user.AccessFailedCount = 0;

        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            AuditAction.Update, "Security", "User", user.Id.ToString(),
            $"Manually unlocked locked account for '{user.Email}'",
            organizationId: user.OrganizationId,
            branchId: user.BranchId,
            userId: CurrentUserService.UserId,
            userEmail: CurrentUserService.Email,
            cancellationToken: cancellationToken);

        return ApiSuccess("Account unlocked successfully.");
    }

    [HttpPost("{id:guid}/admin-reset-password")]
    [RequirePermission(Permissions.UsersResetPassword)]
    public async Task<IActionResult> AdminResetPassword(Guid id, [FromBody] AdminResetPasswordRequestDto request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.NewPassword))
            return ApiError("New password is required.");

        var (isValid, errorMessage) = PasswordPolicyValidator.Validate(request.NewPassword);
        if (!isValid)
            return ApiError(errorMessage ?? "Password does not meet complexity requirements.");

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

        if (user == null)
            return ApiError("User not found.", StatusCodes.Status404NotFound);

        if (!CurrentUserService.IsSuperAdmin && user.OrganizationId != CurrentUserService.OrganizationId)
            return ApiError("Unauthorized access to user outside your organization.", StatusCodes.Status403Forbidden);

        // Security check: Only SuperAdmin can reset a SuperAdmin's password
        if (user.RoleType == UserRoleType.SuperAdmin && !CurrentUserService.IsSuperAdmin)
            return ApiError("Only SuperAdmins can reset passwords for other SuperAdmins.", StatusCodes.Status403Forbidden);

        var (hash, salt) = _passwordHasher.HashPassword(request.NewPassword);
        user.PasswordHash = hash;
        user.PasswordSalt = salt;
        user.IsLocked = false;
        user.LockoutEndUtc = null;
        user.AccessFailedCount = 0;

        // Revoke active refresh tokens
        var tokens = await _context.RefreshTokens
            .Where(t => t.UserId == user.Id && t.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);

        foreach (var t in tokens)
        {
            t.RevokedAtUtc = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            AuditAction.PasswordReset, "Security", "User", user.Id.ToString(),
            $"Administrator reset password for user '{user.Email}'",
            organizationId: user.OrganizationId,
            branchId: user.BranchId,
            userId: CurrentUserService.UserId,
            userEmail: CurrentUserService.Email,
            cancellationToken: cancellationToken);

        return ApiSuccess("User password reset successfully.");
    }
}
