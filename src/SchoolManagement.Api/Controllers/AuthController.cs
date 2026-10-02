using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.Common.Models;
using SchoolManagement.Application.Common.Security;
using SchoolManagement.Domain.Entities.Security;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : BaseApiController
{
    private readonly IApplicationDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly IAuditService _auditService;

    public AuthController(
        IApplicationDbContext context,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        IAuditService auditService)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _auditService = auditService;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.EmailOrUsername) || string.IsNullOrWhiteSpace(request.Password))
        {
            return ApiError("Username/Email and Password are required.", StatusCodes.Status400BadRequest);
        }

        // Search for user
        string search = request.EmailOrUsername.Trim().ToLowerInvariant();
        var user = await _context.Users
            .IgnoreQueryFilters()
            .Where(u => !u.IsDeleted)
            .Include(u => u.Organization)
            .Include(u => u.Branch)
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role!)
                    .ThenInclude(r => r.RolePermissions)
                        .ThenInclude(rp => rp.Permission!)
            .FirstOrDefaultAsync(u =>
                (u.Email.ToLower() == search || u.Username.ToLower() == search),
                cancellationToken);

        if (user == null)
        {
            await _auditService.LogAsync(
                AuditAction.FailedLogin, "Auth", "User", null,
                $"Failed login attempt for '{request.EmailOrUsername}' - user not found",
                cancellationToken: cancellationToken);

            return ApiError("Invalid credentials.", StatusCodes.Status401Unauthorized);
        }

        // Check if locked
        if (user.IsLocked && user.LockoutEndUtc > DateTime.UtcNow)
        {
            await _auditService.LogAsync(
                AuditAction.FailedLogin, "Auth", "User", user.Id.ToString(),
                $"Failed login attempt for locked account '{user.Email}'",
                organizationId: user.OrganizationId,
                userId: user.Id,
                userEmail: user.Email,
                cancellationToken: cancellationToken);

            return ApiError("Account is locked. Please try again later or contact your administrator.", StatusCodes.Status423Locked);
        }

        // Verify password
        bool passwordValid = _passwordHasher.VerifyPassword(request.Password, user.PasswordHash, user.PasswordSalt);
        if (!passwordValid)
        {
            user.AccessFailedCount++;
            if (user.AccessFailedCount >= 5)
            {
                user.IsLocked = true;
                user.LockoutEndUtc = DateTime.UtcNow.AddMinutes(15);
            }

            await _context.SaveChangesAsync(cancellationToken);

            await _auditService.LogAsync(
                AuditAction.FailedLogin, "Auth", "User", user.Id.ToString(),
                $"Failed login attempt (wrong password) for '{user.Email}' (Attempts: {user.AccessFailedCount})",
                organizationId: user.OrganizationId,
                userId: user.Id,
                userEmail: user.Email,
                cancellationToken: cancellationToken);

            return ApiError("Invalid credentials.", StatusCodes.Status401Unauthorized);
        }

        // Check active status
        if (!user.IsActive)
        {
            return ApiError("Account is deactivated. Please contact your administrator.", StatusCodes.Status403Forbidden);
        }

        // Check organization active status
        if (user.Organization != null && !user.Organization.IsActive && user.RoleType != UserRoleType.SuperAdmin)
        {
            return ApiError("Organization subscription is suspended or inactive.", StatusCodes.Status403Forbidden);
        }

        // Reset failed access count & update last login
        user.AccessFailedCount = 0;
        user.IsLocked = false;
        user.LockoutEndUtc = null;
        user.LastLoginAtUtc = DateTime.UtcNow;

        // Auto-heal: Ensure user has system role attached if missing
        if (!user.UserRoles.Any())
        {
            string targetRoleName = user.RoleType.ToString();
            var targetRole = await _context.Roles
                .IgnoreQueryFilters()
                .Include(r => r.RolePermissions)
                    .ThenInclude(rp => rp.Permission)
                .FirstOrDefaultAsync(r => r.IsSystemRole && (r.Name == targetRoleName || (user.RoleType == UserRoleType.OrganizationAdmin && r.Name == "Organization Admin")), cancellationToken);

            if (targetRole != null)
            {
                var newUserRole = new UserRole
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    RoleId = targetRole.Id,
                    Role = targetRole
                };
                user.UserRoles.Add(newUserRole);
                _context.UserRoles.Add(newUserRole);
                await _context.SaveChangesAsync(cancellationToken);
            }
        }

        // Collect all permissions
        var permissions = user.UserRoles
            .SelectMany(ur => ur.Role?.RolePermissions.Select(rp => rp.Permission?.Code) ?? Enumerable.Empty<string?>())
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code!)
            .Distinct()
            .ToList();

        if (user.RoleType == UserRoleType.OrganizationAdmin && !permissions.Any())
        {
            permissions = await _context.Permissions
                .Where(p => p.Code != SchoolManagement.Domain.Constants.Permissions.OrganizationsManage)
                .Select(p => p.Code)
                .ToListAsync(cancellationToken);
        }

        List<string>? enabledModuleNames = null;
        if (user.RoleType != UserRoleType.SuperAdmin)
        {
            enabledModuleNames = await _context.OrganizationModules
                .IgnoreQueryFilters()
                .Where(m => m.OrganizationId == user.OrganizationId && m.IsEnabled && !m.IsDeleted)
                .Select(m => m.ModuleName)
                .ToListAsync(cancellationToken);

            // Filter out permissions belonging to disabled modules
            var coreAdminModules = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Branches", "Users", "Roles", "Audit", "Organizations" };
            permissions = permissions
                .Where(code => {
                    string mod = code.Split('.')[0];
                    return enabledModuleNames.Contains(mod, StringComparer.OrdinalIgnoreCase) || 
                           (user.RoleType == UserRoleType.OrganizationAdmin && coreAdminModules.Contains(mod));
                })
                .ToList();
        }
        else
        {
            enabledModuleNames = await _context.Permissions
                .Select(p => p.Module)
                .Distinct()
                .ToListAsync(cancellationToken);
        }

        // Issue tokens
        string? ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        string accessToken = _tokenService.GenerateAccessToken(user, permissions);
        var refreshToken = _tokenService.GenerateRefreshToken(user.Id, ipAddress);

        _context.RefreshTokens.Add(refreshToken);
        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            AuditAction.Login, "Auth", "User", user.Id.ToString(),
            $"User '{user.Email}' successfully logged in",
            organizationId: user.OrganizationId,
            branchId: user.BranchId,
            userId: user.Id,
            userEmail: user.Email,
            cancellationToken: cancellationToken);

        var userDto = new UserDto(
            user.Id,
            user.OrganizationId,
            user.BranchId,
            user.Username,
            user.Email,
            user.FirstName,
            user.LastName,
            user.FullName,
            user.RoleType,
            user.IsActive,
            permissions,
            user.Organization?.Name,
            user.Organization?.LogoUrl,
            user.Organization?.ThemePrimaryColor ?? "#6C5CE7",
            user.Organization?.ThemeSecondaryColor ?? "#5840BA",
            user.Branch?.Name,
            enabledModuleNames
        );

        return ApiSuccess(AuthResultDto.Succeeded(
            accessToken,
            refreshToken.Token,
            refreshToken.ExpiresAtUtc,
            userDto));
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> RefreshToken([FromBody] string token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
            return ApiError("Refresh token is required.", StatusCodes.Status400BadRequest);

        var existingToken = await _context.RefreshTokens
            .IgnoreQueryFilters()
            .Where(t => !t.IsDeleted)
            .Include(t => t.User!)
                .ThenInclude(u => u.Organization)
            .Include(t => t.User!)
                .ThenInclude(u => u.Branch)
            .Include(t => t.User!)
                .ThenInclude(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role!)
                        .ThenInclude(r => r.RolePermissions)
                            .ThenInclude(rp => rp.Permission!)
            .FirstOrDefaultAsync(t => t.Token == token, cancellationToken);

        if (existingToken == null || !existingToken.IsActiveToken || existingToken.User == null)
            return ApiError("Invalid or expired refresh token.", StatusCodes.Status401Unauthorized);

        var user = existingToken.User;
        if (!user.IsActive)
            return ApiError("User account is inactive.", StatusCodes.Status403Forbidden);

        // Revoke current token and replace
        existingToken.RevokedAtUtc = DateTime.UtcNow;
        string? ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var newRefreshToken = _tokenService.GenerateRefreshToken(user.Id, ipAddress);
        existingToken.ReplacedByToken = newRefreshToken.Token;

        // Auto-heal: Ensure OrganizationAdmin has system role attached if missing
        if (user.RoleType == UserRoleType.OrganizationAdmin && !user.UserRoles.Any())
        {
            var orgAdminRole = await _context.Roles
                .IgnoreQueryFilters()
                .Include(r => r.RolePermissions)
                    .ThenInclude(rp => rp.Permission)
                .FirstOrDefaultAsync(r => r.IsSystemRole && (r.Name == nameof(UserRoleType.OrganizationAdmin) || r.Name == "Organization Admin"), cancellationToken);

            if (orgAdminRole != null)
            {
                var newUserRole = new UserRole
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    RoleId = orgAdminRole.Id,
                    Role = orgAdminRole
                };
                user.UserRoles.Add(newUserRole);
                _context.UserRoles.Add(newUserRole);
                await _context.SaveChangesAsync(cancellationToken);
            }
        }

        var permissions = user.UserRoles
            .SelectMany(ur => ur.Role?.RolePermissions.Select(rp => rp.Permission?.Code) ?? Enumerable.Empty<string?>())
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code!)
            .Distinct()
            .ToList();

        if (user.RoleType == UserRoleType.OrganizationAdmin && !permissions.Any())
        {
            permissions = await _context.Permissions
                .Where(p => p.Code != SchoolManagement.Domain.Constants.Permissions.OrganizationsManage)
                .Select(p => p.Code)
                .ToListAsync(cancellationToken);
        }

        List<string>? enabledModuleNames = null;
        if (user.RoleType != UserRoleType.SuperAdmin)
        {
            enabledModuleNames = await _context.OrganizationModules
                .IgnoreQueryFilters()
                .Where(m => m.OrganizationId == user.OrganizationId && m.IsEnabled && !m.IsDeleted)
                .Select(m => m.ModuleName)
                .ToListAsync(cancellationToken);

            var coreAdminModules = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Branches", "Users", "Roles", "Audit", "Organizations" };
            permissions = permissions
                .Where(code => {
                    string mod = code.Split('.')[0];
                    return enabledModuleNames.Contains(mod, StringComparer.OrdinalIgnoreCase) || 
                           (user.RoleType == UserRoleType.OrganizationAdmin && coreAdminModules.Contains(mod));
                })
                .ToList();
        }
        else
        {
            enabledModuleNames = await _context.Permissions
                .Select(p => p.Module)
                .Distinct()
                .ToListAsync(cancellationToken);
        }

        string newAccessToken = _tokenService.GenerateAccessToken(user, permissions);
        _context.RefreshTokens.Add(newRefreshToken);
        await _context.SaveChangesAsync(cancellationToken);

        var userDto = new UserDto(
            user.Id,
            user.OrganizationId,
            user.BranchId,
            user.Username,
            user.Email,
            user.FirstName,
            user.LastName,
            user.FullName,
            user.RoleType,
            user.IsActive,
            permissions,
            user.Organization?.Name,
            user.Organization?.LogoUrl,
            user.Organization?.ThemePrimaryColor ?? "#6C5CE7",
            user.Organization?.ThemeSecondaryColor ?? "#5840BA",
            user.Branch?.Name,
            enabledModuleNames
        );

        return ApiSuccess(AuthResultDto.Succeeded(
            newAccessToken,
            newRefreshToken.Token,
            newRefreshToken.ExpiresAtUtc,
            userDto));
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout([FromBody] string? refreshToken, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(refreshToken))
        {
            var token = await _context.RefreshTokens.FirstOrDefaultAsync(t => t.Token == refreshToken, cancellationToken);
            if (token != null)
            {
                token.RevokedAtUtc = DateTime.UtcNow;
                await _context.SaveChangesAsync(cancellationToken);
            }
        }

        await _auditService.LogAsync(
            AuditAction.Logout, "Auth", "User", CurrentUserService.UserId?.ToString(),
            $"User logged out",
            organizationId: CurrentUserService.OrganizationId,
            branchId: CurrentUserService.BranchId,
            userId: CurrentUserService.UserId,
            userEmail: CurrentUserService.Email,
            cancellationToken: cancellationToken);

        return ApiSuccess("Logged out successfully.");
    }

    [HttpPost("switch-branch/{branchId:guid}")]
    [Authorize]
    public async Task<IActionResult> SwitchBranch(Guid branchId, CancellationToken cancellationToken)
    {
        if (CurrentUserService.UserId == null)
            return ApiError("Unauthorized", StatusCodes.Status401Unauthorized);

        var user = await _context.Users
            .IgnoreQueryFilters()
            .Where(u => !u.IsDeleted)
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role!)
                    .ThenInclude(r => r.RolePermissions)
                        .ThenInclude(rp => rp.Permission!)
            .FirstOrDefaultAsync(u => u.Id == CurrentUserService.UserId.Value, cancellationToken);

        if (user == null || !user.IsActive)
            return ApiError("User account not found or deactivated.", StatusCodes.Status403Forbidden);

        var branch = await _context.Branches
            .IgnoreQueryFilters()
            .Include(b => b.Organization)
            .FirstOrDefaultAsync(b => b.Id == branchId && !b.IsDeleted, cancellationToken);

        if (branch == null)
            return ApiError("Branch not found.", StatusCodes.Status404NotFound);

        // SuperAdmin can switch to any branch; non-SuperAdmin can only switch within their organization
        if (!CurrentUserService.IsSuperAdmin && branch.OrganizationId != user.OrganizationId)
        {
            return ApiError("Unauthorized access to branch outside your organization.", StatusCodes.Status403Forbidden);
        }

        if (!branch.IsActive)
        {
            return ApiError("The selected branch is inactive.", StatusCodes.Status400BadRequest);
        }

        // Update active branch on the user entity
        user.BranchId = branch.Id;
        user.Branch = branch;
        if (CurrentUserService.IsSuperAdmin && branch.OrganizationId != Guid.Empty)
        {
            user.OrganizationId = branch.OrganizationId;
        }

        var permissions = user.UserRoles
            .SelectMany(ur => ur.Role?.RolePermissions.Select(rp => rp.Permission?.Code) ?? Enumerable.Empty<string?>())
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code!)
            .Distinct()
            .ToList();

        string? ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        string accessToken = _tokenService.GenerateAccessToken(user, permissions);
        var refreshToken = _tokenService.GenerateRefreshToken(user.Id, ipAddress);

        _context.RefreshTokens.Add(refreshToken);
        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            AuditAction.Update, "Auth", "Branch", branch.Id.ToString(),
            $"User '{user.Email}' switched active branch to '{branch.Name}' ({branch.Code})",
            organizationId: user.OrganizationId,
            branchId: branch.Id,
            userId: user.Id,
            userEmail: user.Email,
            cancellationToken: cancellationToken);

        var org = branch.Organization;
        var userDto = new UserDto(
            user.Id,
            user.OrganizationId,
            user.BranchId,
            user.Username,
            user.Email,
            user.FirstName,
            user.LastName,
            user.FullName,
            user.RoleType,
            user.IsActive,
            permissions,
            org?.Name,
            org?.LogoUrl,
            org?.ThemePrimaryColor ?? "#6C5CE7",
            org?.ThemeSecondaryColor ?? "#5840BA",
            branch.Name
        );

        return ApiSuccess(AuthResultDto.Succeeded(
            accessToken,
            refreshToken.Token,
            refreshToken.ExpiresAtUtc,
            userDto), "Active branch switched successfully.");
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequestDto request, CancellationToken cancellationToken)
    {
        if (CurrentUserService.UserId == null)
            return ApiError("Unauthorized", StatusCodes.Status401Unauthorized);

        if (string.IsNullOrWhiteSpace(request.CurrentPassword) || string.IsNullOrWhiteSpace(request.NewPassword))
            return ApiError("Current password and new password are required.");

        if (request.NewPassword != request.ConfirmPassword)
            return ApiError("New password and confirmation password do not match.");

        var (isValid, errorMessage) = PasswordPolicyValidator.Validate(request.NewPassword);
        if (!isValid)
            return ApiError(errorMessage ?? "Password does not meet complexity requirements.");

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == CurrentUserService.UserId.Value, cancellationToken);

        if (user == null)
            return ApiError("User not found.", StatusCodes.Status404NotFound);

        bool currentValid = _passwordHasher.VerifyPassword(request.CurrentPassword, user.PasswordHash, user.PasswordSalt);
        if (!currentValid)
            return ApiError("Current password is incorrect.", StatusCodes.Status400BadRequest);

        var (newHash, newSalt) = _passwordHasher.HashPassword(request.NewPassword);
        user.PasswordHash = newHash;
        user.PasswordSalt = newSalt;

        // Revoke active refresh tokens
        var activeTokens = await _context.RefreshTokens
            .Where(t => t.UserId == user.Id && t.RevokedAtUtc == null && t.ExpiresAtUtc > DateTime.UtcNow)
            .ToListAsync(cancellationToken);

        foreach (var token in activeTokens)
        {
            token.RevokedAtUtc = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            AuditAction.PasswordReset, "Auth", "User", user.Id.ToString(),
            $"User '{user.Email}' changed password successfully",
            organizationId: user.OrganizationId,
            branchId: user.BranchId,
            userId: user.Id,
            userEmail: user.Email,
            cancellationToken: cancellationToken);

        return ApiSuccess("Password has been changed successfully. Please log in with your new password.");
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequestDto request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
            return ApiError("Email is required.");

        var user = await _context.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => !u.IsDeleted && u.Email.ToLower() == request.Email.Trim().ToLower(), cancellationToken);

        if (user != null && user.IsActive)
        {
            byte[] tokenBytes = RandomNumberGenerator.GetBytes(32);
            string token = Convert.ToHexString(tokenBytes);
            user.PasswordResetToken = token;
            user.PasswordResetTokenExpiresAtUtc = DateTime.UtcNow.AddHours(2);
            await _context.SaveChangesAsync(cancellationToken);

            await _auditService.LogAsync(
                AuditAction.SecurityViolation, "Auth", "User", user.Id.ToString(),
                $"Password reset requested for '{user.Email}'",
                organizationId: user.OrganizationId,
                userId: user.Id,
                userEmail: user.Email,
                cancellationToken: cancellationToken);
        }

        // Always return generic success message to prevent user enumeration
        return ApiSuccess("If this email address is registered, instructions to reset your password have been sent.");
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequestDto request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Token) || string.IsNullOrWhiteSpace(request.NewPassword))
            return ApiError("Email, token, and new password are required.");

        if (request.NewPassword != request.ConfirmPassword)
            return ApiError("New password and confirmation password do not match.");

        var (isValid, errorMessage) = PasswordPolicyValidator.Validate(request.NewPassword);
        if (!isValid)
            return ApiError(errorMessage ?? "Password does not meet complexity requirements.");

        var user = await _context.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => !u.IsDeleted && u.Email.ToLower() == request.Email.Trim().ToLower(), cancellationToken);

        if (user == null ||
            string.IsNullOrWhiteSpace(user.PasswordResetToken) ||
            user.PasswordResetToken != request.Token ||
            user.PasswordResetTokenExpiresAtUtc == null ||
            user.PasswordResetTokenExpiresAtUtc < DateTime.UtcNow)
        {
            return ApiError("Invalid or expired password reset token.", StatusCodes.Status400BadRequest);
        }

        var (newHash, newSalt) = _passwordHasher.HashPassword(request.NewPassword);
        user.PasswordHash = newHash;
        user.PasswordSalt = newSalt;
        user.PasswordResetToken = null;
        user.PasswordResetTokenExpiresAtUtc = null;
        user.IsLocked = false;
        user.LockoutEndUtc = null;
        user.AccessFailedCount = 0;

        // Revoke active refresh tokens
        var activeTokens = await _context.RefreshTokens
            .Where(t => t.UserId == user.Id && t.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);

        foreach (var token in activeTokens)
        {
            token.RevokedAtUtc = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            AuditAction.PasswordReset, "Auth", "User", user.Id.ToString(),
            $"User '{user.Email}' successfully reset password with token",
            organizationId: user.OrganizationId,
            userId: user.Id,
            userEmail: user.Email,
            cancellationToken: cancellationToken);

        return ApiSuccess("Password has been reset successfully. You may now log in with your new password.");
    }

    [HttpGet("profile")]
    [Authorize]
    public async Task<IActionResult> GetProfile(CancellationToken cancellationToken)
    {
        if (CurrentUserService.UserId == null)
            return ApiError("Unauthorized", StatusCodes.Status401Unauthorized);

        var user = await _context.Users
            .Include(u => u.Organization)
            .Include(u => u.Branch)
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role!)
                    .ThenInclude(r => r.RolePermissions)
                        .ThenInclude(rp => rp.Permission!)
            .FirstOrDefaultAsync(u => u.Id == CurrentUserService.UserId.Value, cancellationToken);

        if (user == null)
            return ApiError("User not found.", StatusCodes.Status404NotFound);

        var permissions = user.UserRoles
            .SelectMany(ur => ur.Role?.RolePermissions.Select(rp => rp.Permission?.Code) ?? Enumerable.Empty<string?>())
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code!)
            .Distinct()
            .ToList();

        var userDto = new UserDto(
            user.Id,
            user.OrganizationId,
            user.BranchId,
            user.Username,
            user.Email,
            user.FirstName,
            user.LastName,
            user.FullName,
            user.RoleType,
            user.IsActive,
            permissions,
            user.Organization?.Name,
            user.Organization?.LogoUrl,
            user.Organization?.ThemePrimaryColor ?? "#6C5CE7",
            user.Organization?.ThemeSecondaryColor ?? "#5840BA",
            user.Branch?.Name
        );

        return ApiSuccess(userDto);
    }

    [HttpPut("profile")]
    [Authorize]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequestDto request, CancellationToken cancellationToken)
    {
        if (CurrentUserService.UserId == null)
            return ApiError("Unauthorized", StatusCodes.Status401Unauthorized);

        if (string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.LastName))
            return ApiError("First name and last name are required.");

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == CurrentUserService.UserId.Value, cancellationToken);

        if (user == null)
            return ApiError("User not found.", StatusCodes.Status404NotFound);

        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();
        user.PhoneNumber = request.PhoneNumber?.Trim();

        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            AuditAction.Update, "Auth", "User", user.Id.ToString(),
            $"User '{user.Email}' updated personal profile details",
            organizationId: user.OrganizationId,
            branchId: user.BranchId,
            userId: user.Id,
            userEmail: user.Email,
            cancellationToken: cancellationToken);

        return ApiSuccess("Profile updated successfully.");
    }
}
