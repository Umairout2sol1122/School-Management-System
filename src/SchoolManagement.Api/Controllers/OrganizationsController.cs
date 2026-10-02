using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Api.Authorization;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.Common.Models;
using SchoolManagement.Application.Common.Security;
using SchoolManagement.Domain.Constants;
using SchoolManagement.Domain.Entities.Security;
using SchoolManagement.Domain.Entities.Tenant;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OrganizationsController : BaseApiController
{
    private readonly IApplicationDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IAuditService _auditService;
    private readonly IWebHostEnvironment? _webHostEnvironment;

    public static readonly List<(string ModuleName, string DisplayName, string Description)> PlatformModules = new()
    {
        ("Students", "Student Information & Admissions", "Enrollment, student profiles, emergency contacts, and class rosters"),
        ("Teachers", "Faculty & Staff Management", "Teacher profiles, departmental assignments, and designations"),
        ("Academics", "Curriculum & Academic Structure", "Courses, subjects, academic terms, and syllabus tracking"),
        ("Branches", "Campus & Branch Management", "Multi-campus locations, branch capacity, and branch administration"),
        ("Users", "User Management & Accounts", "Faculty, staff, student, parent, and branch administrator accounts"),
        ("Roles", "Roles & Access Control", "Custom role creation and granular permission delegation"),
        ("Attendance", "Daily Attendance Management", "Student attendance check-ins, leaves, and absence alerts"),
        ("Fees", "Fee Collections & Billing", "Tuition challans, billing cycles, fee vouchers, and payment records"),
        ("Payroll", "Staff Payroll & Compensation", "Salary structures, allowance, deductions, FBR taxes, and payslips"),
        ("Library", "Library & Book Circulation", "Catalog, book issues, returns, fines, and ISBN tracking"),
        ("Transport", "Transport & Fleet Management", "Vehicles, bus routes, driver assignments, and transport tracking"),
        ("Certificates", "Certificates & Clearance", "Leaving certificates, character certificates, QR code verification"),
        ("Exams", "Examinations & Grading", "Exam schedules, grade books, report cards, and marks registers"),
        ("Timetable", "Class Timetables & Schedules", "Weekly periods, teacher schedules, and room allocations"),
        ("Homework", "Assignments & Homework", "Homework distribution, submission tracking, and grading"),
        ("Communication", "Announcements & Messaging", "SMS/Email notifications, broadcast notices, and parent alerts"),
        ("Reports", "Analytics & Reports", "Comprehensive institutional reporting, performance metrics, and exports"),
        ("Audit", "Audit & Security Logs", "Institutional activity tracking, security logs, and compliance records"),
        ("Organizations", "Organization Profile & Settings", "Institutional profile, branding, academic sessions, and settings")
    };

    public OrganizationsController(
        IApplicationDbContext context,
        IPasswordHasher passwordHasher,
        IAuditService auditService,
        IWebHostEnvironment? webHostEnvironment = null)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _auditService = auditService;
        _webHostEnvironment = webHostEnvironment;
    }

    [HttpGet]
    [RequirePermission(Permissions.OrganizationsView)]
    public async Task<IActionResult> GetOrganizations([FromQuery] string? search, [FromQuery] bool? isActive, CancellationToken cancellationToken)
    {
        var query = _context.Organizations
            .Include(o => o.Branches)
            .AsQueryable();

        // Non-SuperAdmin can only see their own organization
        if (!CurrentUserService.IsSuperAdmin)
        {
            if (CurrentUserService.OrganizationId == null)
                return ApiError("Tenant context required.", StatusCodes.Status403Forbidden);

            query = query.Where(o => o.Id == CurrentUserService.OrganizationId.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            string term = search.Trim().ToLower();
            query = query.Where(o => o.Name.ToLower().Contains(term) || o.Code.ToLower().Contains(term) || o.Email.ToLower().Contains(term));
        }

        if (isActive.HasValue)
        {
            query = query.Where(o => o.IsActive == isActive.Value);
        }

        var orgs = await query
            .OrderBy(o => o.Name)
            .Select(o => new OrganizationSummaryDto(
                o.Id,
                o.Name,
                o.Code,
                o.Email,
                o.Phone,
                o.IsActive,
                o.Branches.Count(b => !b.IsDeleted),
                _context.Users.Count(u => u.OrganizationId == o.Id && !u.IsDeleted),
                o.ThemePrimaryColor,
                o.ThemeSecondaryColor,
                o.CreatedAtUtc,
                _context.Users.Where(u => u.OrganizationId == o.Id && u.RoleType == UserRoleType.OrganizationAdmin && !u.IsDeleted).OrderBy(u => u.CreatedAtUtc).Select(u => u.FullName).FirstOrDefault(),
                _context.Users.Where(u => u.OrganizationId == o.Id && u.RoleType == UserRoleType.OrganizationAdmin && !u.IsDeleted).OrderBy(u => u.CreatedAtUtc).Select(u => u.Email).FirstOrDefault(),
                _context.Users.Where(u => u.OrganizationId == o.Id && u.RoleType == UserRoleType.OrganizationAdmin && !u.IsDeleted).OrderBy(u => u.CreatedAtUtc).Select(u => (Guid?)u.Id).FirstOrDefault(),
                _context.OrganizationModules.Count(m => m.OrganizationId == o.Id && m.IsEnabled && !m.IsDeleted),
                o.LogoUrl,
                o.MaxBranches
            ))
            .ToListAsync(cancellationToken);

        return ApiSuccess(orgs);
    }

    [HttpGet("my-school")]
    [RequirePermission(Permissions.OrganizationsView)]
    public async Task<IActionResult> GetMySchool(CancellationToken cancellationToken)
    {
        if (CurrentUserService.OrganizationId == null)
            return ApiError("Tenant context required.", StatusCodes.Status400BadRequest);

        var org = await _context.Organizations
            .Include(o => o.Branches)
            .FirstOrDefaultAsync(o => o.Id == CurrentUserService.OrganizationId.Value, cancellationToken);

        if (org == null)
            return ApiError("Organization not found.", StatusCodes.Status404NotFound);

        int branchesCount = org.Branches.Count(b => !b.IsDeleted);
        int usersCount = await _context.Users.CountAsync(u => u.OrganizationId == org.Id && !u.IsDeleted, cancellationToken);

        var profile = new SchoolProfileDto(
            org.Id,
            org.Name,
            org.Code,
            org.Email,
            org.Phone,
            org.Address,
            org.LogoUrl,
            org.ThemePrimaryColor,
            org.ThemeSecondaryColor,
            org.ThemeDarkMode,
            org.IsActive,
            branchesCount,
            usersCount,
            org.CreatedAtUtc,
            org.MaxBranches
        );

        return ApiSuccess(profile);
    }

    [HttpPut("my-school")]
    public async Task<IActionResult> UpdateMySchool([FromBody] UpdateSchoolProfileRequestDto request, CancellationToken cancellationToken)
    {
        if (CurrentUserService.OrganizationId == null)
            return ApiError("Tenant context required.", StatusCodes.Status400BadRequest);

        if (!CurrentUserService.IsSuperAdmin && CurrentUserService.RoleType != UserRoleType.OrganizationAdmin)
        {
            return ApiError("Only Organization Administrators can update school profile and branding.", StatusCodes.Status403Forbidden);
        }

        if (string.IsNullOrWhiteSpace(request.Name))
            return ApiError("School name is required.");

        var org = await _context.Organizations.FirstOrDefaultAsync(o => o.Id == CurrentUserService.OrganizationId.Value, cancellationToken);
        if (org == null)
            return ApiError("Organization not found.", StatusCodes.Status404NotFound);

        org.Name = request.Name.Trim();
        org.Email = request.Email?.Trim() ?? string.Empty;
        org.Phone = request.Phone?.Trim() ?? string.Empty;
        org.Address = request.Address?.Trim() ?? string.Empty;
        org.LogoUrl = request.LogoUrl?.Trim();
        if (!string.IsNullOrWhiteSpace(request.ThemePrimaryColor))
            org.ThemePrimaryColor = request.ThemePrimaryColor.Trim();
        if (!string.IsNullOrWhiteSpace(request.ThemeSecondaryColor))
            org.ThemeSecondaryColor = request.ThemeSecondaryColor.Trim();
        org.ThemeDarkMode = request.ThemeDarkMode;

        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            AuditAction.Update, "Organizations", "Organization", org.Id.ToString(),
            $"Organization Admin updated school profile and branding for '{org.Name}'",
            organizationId: org.Id,
            userId: CurrentUserService.UserId,
            userEmail: CurrentUserService.Email,
            cancellationToken: cancellationToken);

        int branchesCount = await _context.Branches.CountAsync(b => b.OrganizationId == org.Id && !b.IsDeleted, cancellationToken);
        int usersCount = await _context.Users.CountAsync(u => u.OrganizationId == org.Id && !u.IsDeleted, cancellationToken);

        var profile = new SchoolProfileDto(
            org.Id,
            org.Name,
            org.Code,
            org.Email,
            org.Phone,
            org.Address,
            org.LogoUrl,
            org.ThemePrimaryColor,
            org.ThemeSecondaryColor,
            org.ThemeDarkMode,
            org.IsActive,
            branchesCount,
            usersCount,
            org.CreatedAtUtc,
            org.MaxBranches
        );

        return ApiSuccess(profile, "School profile and branding updated successfully.");
    }

    [HttpGet("{id:guid}")]
    [RequirePermission(Permissions.OrganizationsView)]
    public async Task<IActionResult> GetOrganizationById(Guid id, CancellationToken cancellationToken)
    {
        if (!CurrentUserService.IsSuperAdmin && CurrentUserService.OrganizationId != id)
        {
            return ApiError("Unauthorized access to organization details.", StatusCodes.Status403Forbidden);
        }

        var org = await _context.Organizations
            .Include(o => o.Branches)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

        if (org == null)
            return ApiError("Organization not found.", StatusCodes.Status404NotFound);

        var branches = org.Branches
            .Where(b => !b.IsDeleted)
            .OrderBy(b => b.IsMainBranch ? 0 : 1)
            .ThenBy(b => b.Name)
            .Select(b => new BranchDto(
                b.Id,
                b.OrganizationId,
                org.Name,
                b.Name,
                b.Code,
                b.Address,
                b.Phone,
                b.Email,
                b.IsMainBranch,
                b.IsActive,
                b.CreatedAtUtc
            ))
            .ToList();

        // Org Admin details
        var adminUser = await _context.Users
            .Where(u => u.OrganizationId == id && u.RoleType == UserRoleType.OrganizationAdmin && !u.IsDeleted)
            .OrderBy(u => u.CreatedAtUtc)
            .Select(u => new AdminUserSummaryDto(
                u.Id,
                u.FullName,
                u.Email,
                u.Username,
                u.PhoneNumber,
                u.IsActive,
                u.LastLoginAtUtc
            ))
            .FirstOrDefaultAsync(cancellationToken);

        // Modules configuration
        var orgModules = await _context.OrganizationModules
            .IgnoreQueryFilters()
            .Where(m => m.OrganizationId == id && !m.IsDeleted)
            .ToListAsync(cancellationToken);

        var moduleItems = PlatformModules.Select(pm =>
        {
            var match = orgModules.FirstOrDefault(m => string.Equals(m.ModuleName, pm.ModuleName, StringComparison.OrdinalIgnoreCase));
            return new OrganizationModuleItemDto(
                pm.ModuleName,
                pm.DisplayName,
                pm.Description,
                match?.IsEnabled ?? true
            );
        }).ToList();

        int studentsCount = await _context.Students.CountAsync(s => s.OrganizationId == id && !s.IsDeleted, cancellationToken);
        int staffCount = await _context.StaffProfiles.CountAsync(s => s.OrganizationId == id && !s.IsDeleted, cancellationToken);
        int totalUsers = await _context.Users.CountAsync(u => u.OrganizationId == id && !u.IsDeleted, cancellationToken);

        var details = new OrganizationDetailsDto(
            org.Id,
            org.Name,
            org.Code,
            org.Email,
            org.Phone,
            org.Address,
            org.LogoUrl,
            org.ThemePrimaryColor,
            org.ThemeSecondaryColor,
            org.ThemeDarkMode,
            org.IsActive,
            org.CreatedAtUtc,
            branches,
            adminUser,
            moduleItems,
            studentsCount,
            staffCount,
            totalUsers,
            org.UpdatedAtUtc,
            org.MaxBranches
        );

        return ApiSuccess(details);
    }

    [HttpPost]
    [RequirePermission(Permissions.OrganizationsManage)]
    public async Task<IActionResult> CreateOrganization([FromBody] CreateOrganizationRequestDto request, CancellationToken cancellationToken)
    {
        if (!CurrentUserService.IsSuperAdmin)
        {
            return ApiError("Only platform SuperAdmins can register new organizations.", StatusCodes.Status403Forbidden);
        }

        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Code))
            return ApiError("Organization name and code are required.");

        if (request.MaxBranches.HasValue && request.MaxBranches.Value <= 0)
            return ApiError("Allowed branches quota must be at least 1.");

        if (string.IsNullOrWhiteSpace(request.InitialAdminEmail) || string.IsNullOrWhiteSpace(request.InitialAdminPassword))
            return ApiError("Initial administrator email and password are required.");

        var (isPassValid, passError) = PasswordPolicyValidator.Validate(request.InitialAdminPassword);
        if (!isPassValid)
            return ApiError(passError ?? "Administrator password does not meet complexity requirements.");

        string normCode = request.Code.Trim().ToUpperInvariant();
        bool codeExists = await _context.Organizations.AnyAsync(o => o.Code.ToUpper() == normCode, cancellationToken);
        if (codeExists)
            return ApiError($"An organization with code '{request.Code}' already exists.");

        // 1. Create Organization
        var org = new Organization
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Code = normCode,
            Email = request.Email?.Trim() ?? string.Empty,
            Phone = request.Phone?.Trim() ?? string.Empty,
            Address = request.Address?.Trim() ?? string.Empty,
            LogoUrl = request.LogoUrl?.Trim(),
            ThemePrimaryColor = string.IsNullOrWhiteSpace(request.ThemePrimaryColor) ? "#6C5CE7" : request.ThemePrimaryColor.Trim(),
            ThemeSecondaryColor = string.IsNullOrWhiteSpace(request.ThemeSecondaryColor) ? "#5840BA" : request.ThemeSecondaryColor.Trim(),
            ThemeDarkMode = true,
            MaxBranches = request.MaxBranches.HasValue && request.MaxBranches.Value > 0 ? request.MaxBranches.Value : 5,
            IsActive = true
        };

        // 2. Automatically create Default Main Branch
        var mainBranch = new Branch
        {
            Id = Guid.NewGuid(),
            OrganizationId = org.Id,
            Name = $"{org.Name} - Main Campus",
            Code = $"{normCode}-MAIN",
            Address = org.Address,
            Phone = org.Phone,
            Email = org.Email,
            IsMainBranch = true,
            IsActive = true
        };
        org.Branches.Add(mainBranch);

        // 3. Create Initial Organization Admin user
        var (hash, salt) = _passwordHasher.HashPassword(request.InitialAdminPassword);
        var adminUser = new User
        {
            Id = Guid.NewGuid(),
            OrganizationId = org.Id,
            BranchId = mainBranch.Id,
            Username = string.IsNullOrWhiteSpace(request.InitialAdminUsername) ? request.InitialAdminEmail.Trim() : request.InitialAdminUsername.Trim(),
            Email = request.InitialAdminEmail.Trim(),
            NormalizedEmail = request.InitialAdminEmail.Trim().ToUpperInvariant(),
            FirstName = string.IsNullOrWhiteSpace(request.InitialAdminFirstName) ? "Admin" : request.InitialAdminFirstName.Trim(),
            LastName = string.IsNullOrWhiteSpace(request.InitialAdminLastName) ? "User" : request.InitialAdminLastName.Trim(),
            PasswordHash = hash,
            PasswordSalt = salt,
            RoleType = UserRoleType.OrganizationAdmin,
            IsActive = true
        };

        // 4. Assign system OrgAdmin role if available
        var orgAdminRole = await _context.Roles
            .FirstOrDefaultAsync(r => r.IsSystemRole && (r.Name == nameof(UserRoleType.OrganizationAdmin) || r.Name == "Organization Admin"), cancellationToken);

        if (orgAdminRole != null)
        {
            adminUser.UserRoles.Add(new UserRole
            {
                Id = Guid.NewGuid(),
                UserId = adminUser.Id,
                RoleId = orgAdminRole.Id
            });
        }

        // 5. Automatically seed default Organization Modules
        foreach (var pm in PlatformModules)
        {
            org.Modules.Add(new OrganizationModule
            {
                Id = Guid.NewGuid(),
                OrganizationId = org.Id,
                ModuleName = pm.ModuleName,
                IsEnabled = true
            });
        }

        _context.Organizations.Add(org);
        _context.Users.Add(adminUser);
        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            AuditAction.Create, "SuperAdmin", "Organization", org.Id.ToString(),
            $"Created new organization '{org.Name}' ({org.Code}) with main branch and admin '{adminUser.Email}'",
            organizationId: org.Id,
            userId: CurrentUserService.UserId,
            userEmail: CurrentUserService.Email,
            cancellationToken: cancellationToken);

        return ApiSuccess(new { organizationId = org.Id, code = org.Code, adminEmail = adminUser.Email },
            "Organization, main branch, and administrator account created successfully.");
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(Permissions.OrganizationsManage)]
    public async Task<IActionResult> UpdateOrganization(Guid id, [FromBody] UpdateOrganizationRequestDto request, CancellationToken cancellationToken)
    {
        if (!CurrentUserService.IsSuperAdmin && CurrentUserService.OrganizationId != id)
        {
            return ApiError("Unauthorized access to modify organization details.", StatusCodes.Status403Forbidden);
        }

        var org = await _context.Organizations.FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
        if (org == null)
            return ApiError("Organization not found.", StatusCodes.Status404NotFound);

        if (string.IsNullOrWhiteSpace(request.Name))
            return ApiError("Organization name is required.");

        org.Name = request.Name.Trim();
        org.Email = request.Email?.Trim() ?? string.Empty;
        org.Phone = request.Phone?.Trim() ?? string.Empty;
        org.Address = request.Address?.Trim() ?? string.Empty;
        org.LogoUrl = request.LogoUrl?.Trim();
        if (!string.IsNullOrWhiteSpace(request.ThemePrimaryColor))
            org.ThemePrimaryColor = request.ThemePrimaryColor.Trim();
        if (!string.IsNullOrWhiteSpace(request.ThemeSecondaryColor))
            org.ThemeSecondaryColor = request.ThemeSecondaryColor.Trim();
        org.ThemeDarkMode = request.ThemeDarkMode;
        if (CurrentUserService.IsSuperAdmin && request.MaxBranches.HasValue)
        {
            if (request.MaxBranches.Value <= 0)
                return ApiError("Allowed branches quota must be at least 1.");
            org.MaxBranches = request.MaxBranches.Value;
        }

        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            AuditAction.Update, "SuperAdmin", "Organization", org.Id.ToString(),
            $"Updated organization '{org.Name}' details and branding",
            organizationId: org.Id,
            userId: CurrentUserService.UserId,
            userEmail: CurrentUserService.Email,
            cancellationToken: cancellationToken);

        return ApiSuccess("Organization updated successfully.");
    }

    [HttpPatch("{id:guid}/toggle-status")]
    [RequirePermission(Permissions.OrganizationsManage)]
    public async Task<IActionResult> ToggleStatus(Guid id, CancellationToken cancellationToken)
    {
        if (!CurrentUserService.IsSuperAdmin)
        {
            return ApiError("Only platform SuperAdmins can toggle organization subscription status.", StatusCodes.Status403Forbidden);
        }

        var org = await _context.Organizations.FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
        if (org == null)
            return ApiError("Organization not found.", StatusCodes.Status404NotFound);

        org.IsActive = !org.IsActive;
        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            AuditAction.Update, "SuperAdmin", "Organization", org.Id.ToString(),
            $"Toggled organization '{org.Name}' status to Active={org.IsActive}",
            organizationId: org.Id,
            userId: CurrentUserService.UserId,
            userEmail: CurrentUserService.Email,
            cancellationToken: cancellationToken);

        return ApiSuccess(new { id = org.Id, isActive = org.IsActive },
            $"Organization '{org.Name}' status updated to {(org.IsActive ? "Active" : "Suspended")}.");
    }

    [HttpGet("{id:guid}/stats")]
    [RequirePermission(Permissions.OrganizationsView)]
    public async Task<IActionResult> GetOrganizationStats(Guid id, CancellationToken cancellationToken)
    {
        if (!CurrentUserService.IsSuperAdmin && CurrentUserService.OrganizationId != id)
        {
            return ApiError("Unauthorized access to organization stats.", StatusCodes.Status403Forbidden);
        }

        var org = await _context.Organizations.FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
        if (org == null)
            return ApiError("Organization not found.", StatusCodes.Status404NotFound);

        int branchesCount = await _context.Branches.CountAsync(b => b.OrganizationId == id && !b.IsDeleted, cancellationToken);
        int usersCount = await _context.Users.CountAsync(u => u.OrganizationId == id && !u.IsDeleted, cancellationToken);

        return ApiSuccess(new
        {
            organizationId = org.Id,
            name = org.Name,
            code = org.Code,
            isActive = org.IsActive,
            branchesCount,
            usersCount
        });
    }

    [HttpPost("{id:guid}/logo")]
    [RequirePermission(Permissions.OrganizationsManage)]
    public async Task<IActionResult> UploadLogo(Guid id, IFormFile? file, CancellationToken cancellationToken)
    {
        if (!CurrentUserService.IsSuperAdmin && CurrentUserService.OrganizationId != id)
        {
            return ApiError("Unauthorized access to update organization logo.", StatusCodes.Status403Forbidden);
        }

        if (file == null || file.Length == 0)
        {
            return ApiError("Please select a valid image file to upload.");
        }

        // Validate max 2MB
        const long maxBytes = 2 * 1024 * 1024;
        if (file.Length > maxBytes)
        {
            return ApiError("File size exceeds 2MB limit. Please choose a smaller image.");
        }

        // Validate extension
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var allowedExts = new[] { ".png", ".jpg", ".jpeg", ".webp", ".svg" };
        if (!allowedExts.Contains(ext))
        {
            return ApiError("Unsupported file type. Supported formats: PNG, JPG, JPEG, WEBP, SVG.");
        }

        var org = await _context.Organizations.FirstOrDefaultAsync(o => o.Id == id && !o.IsDeleted, cancellationToken);
        if (org == null)
            return ApiError("Organization not found.", StatusCodes.Status404NotFound);

        // Prepare directory
        string webRoot = _webHostEnvironment?.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        string logosDir = Path.Combine(webRoot, "uploads", "logos");
        Directory.CreateDirectory(logosDir);

        string fileName = $"{org.Id}_{DateTime.UtcNow.Ticks}{ext}";
        string filePath = Path.Combine(logosDir, fileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream, cancellationToken);
        }

        // Remove old physical logo file if exists
        if (!string.IsNullOrWhiteSpace(org.LogoUrl) && org.LogoUrl.StartsWith("/uploads/logos/"))
        {
            string oldFileName = Path.GetFileName(org.LogoUrl);
            string oldFilePath = Path.Combine(logosDir, oldFileName);
            if (System.IO.File.Exists(oldFilePath))
            {
                try { System.IO.File.Delete(oldFilePath); } catch { /* ignore cleanup error */ }
            }
        }

        org.LogoUrl = $"/uploads/logos/{fileName}";
        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            AuditAction.Update, "SuperAdmin", "Organization", org.Id.ToString(),
            $"Updated logo for organization '{org.Name}'",
            organizationId: org.Id,
            userId: CurrentUserService.UserId,
            userEmail: CurrentUserService.Email,
            cancellationToken: cancellationToken);

        return ApiSuccess(new { logoUrl = org.LogoUrl }, "Organization logo updated successfully.");
    }

    [HttpDelete("{id:guid}/logo")]
    [RequirePermission(Permissions.OrganizationsManage)]
    public async Task<IActionResult> RemoveLogo(Guid id, CancellationToken cancellationToken)
    {
        if (!CurrentUserService.IsSuperAdmin && CurrentUserService.OrganizationId != id)
        {
            return ApiError("Unauthorized access to delete organization logo.", StatusCodes.Status403Forbidden);
        }

        var org = await _context.Organizations.FirstOrDefaultAsync(o => o.Id == id && !o.IsDeleted, cancellationToken);
        if (org == null)
            return ApiError("Organization not found.", StatusCodes.Status404NotFound);

        if (!string.IsNullOrWhiteSpace(org.LogoUrl) && org.LogoUrl.StartsWith("/uploads/logos/"))
        {
            string webRoot = _webHostEnvironment?.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            string logosDir = Path.Combine(webRoot, "uploads", "logos");
            string fileName = Path.GetFileName(org.LogoUrl);
            string filePath = Path.Combine(logosDir, fileName);
            if (System.IO.File.Exists(filePath))
            {
                try { System.IO.File.Delete(filePath); } catch { /* ignore */ }
            }
        }

        org.LogoUrl = null;
        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            AuditAction.Update, "SuperAdmin", "Organization", org.Id.ToString(),
            $"Removed logo for organization '{org.Name}'",
            organizationId: org.Id,
            userId: CurrentUserService.UserId,
            userEmail: CurrentUserService.Email,
            cancellationToken: cancellationToken);

        return ApiSuccess("Organization logo removed successfully. System will use default branding.");
    }

    [HttpGet("{id:guid}/modules")]
    [RequirePermission(Permissions.OrganizationsView)]
    public async Task<IActionResult> GetOrganizationModules(Guid id, CancellationToken cancellationToken)
    {
        if (!CurrentUserService.IsSuperAdmin && CurrentUserService.OrganizationId != id)
        {
            return ApiError("Unauthorized access to organization module configuration.", StatusCodes.Status403Forbidden);
        }

        var orgExists = await _context.Organizations.AnyAsync(o => o.Id == id && !o.IsDeleted, cancellationToken);
        if (!orgExists)
            return ApiError("Organization not found.", StatusCodes.Status404NotFound);

        var orgModules = await _context.OrganizationModules
            .IgnoreQueryFilters()
            .Where(m => m.OrganizationId == id && !m.IsDeleted)
            .ToListAsync(cancellationToken);

        var result = PlatformModules.Select(pm =>
        {
            var match = orgModules.FirstOrDefault(m => string.Equals(m.ModuleName, pm.ModuleName, StringComparison.OrdinalIgnoreCase));
            return new OrganizationModuleItemDto(
                pm.ModuleName,
                pm.DisplayName,
                pm.Description,
                match?.IsEnabled ?? true
            );
        }).ToList();

        return ApiSuccess(result);
    }

    [HttpPut("{id:guid}/modules")]
    [RequirePermission(Permissions.OrganizationsManage)]
    public async Task<IActionResult> UpdateOrganizationModules(Guid id, [FromBody] UpdateOrganizationModulesRequestDto request, CancellationToken cancellationToken)
    {
        if (!CurrentUserService.IsSuperAdmin)
        {
            return ApiError("Only platform SuperAdmins can modify organization module access.", StatusCodes.Status403Forbidden);
        }

        var org = await _context.Organizations.FirstOrDefaultAsync(o => o.Id == id && !o.IsDeleted, cancellationToken);
        if (org == null)
            return ApiError("Organization not found.", StatusCodes.Status404NotFound);

        var existingModules = await _context.OrganizationModules
            .IgnoreQueryFilters()
            .Where(m => m.OrganizationId == id)
            .ToListAsync(cancellationToken);

        var changesList = new List<string>();

        foreach (var mod in request.Modules)
        {
            var match = existingModules.FirstOrDefault(m => string.Equals(m.ModuleName, mod.ModuleName, StringComparison.OrdinalIgnoreCase));
            if (match != null)
            {
                if (match.IsEnabled != mod.IsEnabled)
                {
                    match.IsEnabled = mod.IsEnabled;
                    changesList.Add($"{mod.ModuleName}: {(mod.IsEnabled ? "Enabled" : "Disabled")}");
                }
            }
            else
            {
                var newMod = new OrganizationModule
                {
                    Id = Guid.NewGuid(),
                    OrganizationId = id,
                    ModuleName = mod.ModuleName,
                    IsEnabled = mod.IsEnabled
                };
                _context.OrganizationModules.Add(newMod);
                changesList.Add($"{mod.ModuleName}: {(mod.IsEnabled ? "Enabled" : "Disabled")}");
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        if (changesList.Count > 0)
        {
            await _auditService.LogAsync(
                AuditAction.Update, "SuperAdmin", "OrganizationModule", id.ToString(),
                $"SuperAdmin updated module access for '{org.Name}': {string.Join(", ", changesList)}",
                organizationId: id,
                userId: CurrentUserService.UserId,
                userEmail: CurrentUserService.Email,
                cancellationToken: cancellationToken);
        }

        return ApiSuccess("Module access updated successfully.");
    }

    [HttpPost("{id:guid}/admin/reset-password")]
    [RequirePermission(Permissions.OrganizationsManage)]
    public async Task<IActionResult> ResetAdminPassword(Guid id, [FromBody] ResetAdminPasswordRequestDto request, CancellationToken cancellationToken)
    {
        if (!CurrentUserService.IsSuperAdmin)
        {
            return ApiError("Only platform SuperAdmins can reset organization administrator passwords.", StatusCodes.Status403Forbidden);
        }

        if (string.IsNullOrWhiteSpace(request.NewPassword))
        {
            return ApiError("New password is required.");
        }

        var (isValid, passError) = PasswordPolicyValidator.Validate(request.NewPassword);
        if (!isValid)
        {
            return ApiError(passError ?? "Password does not meet complexity requirements.");
        }

        var org = await _context.Organizations.FirstOrDefaultAsync(o => o.Id == id && !o.IsDeleted, cancellationToken);
        if (org == null)
            return ApiError("Organization not found.", StatusCodes.Status404NotFound);

        var adminUser = await _context.Users
            .Where(u => u.OrganizationId == id && u.RoleType == UserRoleType.OrganizationAdmin && !u.IsDeleted)
            .OrderBy(u => u.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (adminUser == null)
        {
            return ApiError("No administrator account found for this organization.", StatusCodes.Status404NotFound);
        }

        var (hash, salt) = _passwordHasher.HashPassword(request.NewPassword);
        adminUser.PasswordHash = hash;
        adminUser.PasswordSalt = salt;
        adminUser.AccessFailedCount = 0;
        adminUser.IsLocked = false;
        adminUser.LockoutEndUtc = null;

        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            AuditAction.PasswordReset, "SuperAdmin", "User", adminUser.Id.ToString(),
            $"SuperAdmin reset password for organization administrator '{adminUser.Email}' ({org.Name})",
            organizationId: org.Id,
            userId: CurrentUserService.UserId,
            userEmail: CurrentUserService.Email,
            cancellationToken: cancellationToken);

        return ApiSuccess($"Administrator password for '{adminUser.Email}' has been successfully reset.");
    }

    [HttpGet("superadmin-dashboard")]
    [RequirePermission(Permissions.OrganizationsView)]
    public async Task<IActionResult> GetSuperAdminDashboard(CancellationToken cancellationToken)
    {
        if (!CurrentUserService.IsSuperAdmin)
        {
            return ApiError("SuperAdmin access required.", StatusCodes.Status403Forbidden);
        }

        int totalOrgs = await _context.Organizations.CountAsync(o => !o.IsDeleted, cancellationToken);
        int activeOrgs = await _context.Organizations.CountAsync(o => !o.IsDeleted && o.IsActive, cancellationToken);
        int inactiveOrgs = await _context.Organizations.CountAsync(o => !o.IsDeleted && !o.IsActive, cancellationToken);
        int totalAdmins = await _context.Users.CountAsync(u => !u.IsDeleted && u.RoleType == UserRoleType.OrganizationAdmin, cancellationToken);
        int totalUsers = await _context.Users.CountAsync(u => !u.IsDeleted, cancellationToken);
        int totalStudents = await _context.Students.CountAsync(s => !s.IsDeleted, cancellationToken);
        int totalTeachers = await _context.StaffProfiles.CountAsync(s => !s.IsDeleted, cancellationToken);

        var recentCreated = await _context.Organizations
            .Where(o => !o.IsDeleted)
            .OrderByDescending(o => o.CreatedAtUtc)
            .Take(5)
            .Select(o => new RecentOrganizationSummaryDto(
                o.Id,
                o.Name,
                o.Code,
                o.Email,
                o.IsActive,
                _context.Users.Where(u => u.OrganizationId == o.Id && u.RoleType == UserRoleType.OrganizationAdmin && !u.IsDeleted).OrderBy(u => u.CreatedAtUtc).Select(u => u.Email).FirstOrDefault(),
                o.CreatedAtUtc,
                o.LogoUrl
            ))
            .ToListAsync(cancellationToken);

        var activeOrgIds = await _context.AuditLogs
            .Where(a => a.OrganizationId != null && a.OrganizationId != Guid.Empty)
            .OrderByDescending(a => a.CreatedAtUtc)
            .Select(a => a.OrganizationId!.Value)
            .Distinct()
            .Take(5)
            .ToListAsync(cancellationToken);

        var recentActiveOrgs = await _context.Organizations
            .Where(o => !o.IsDeleted && activeOrgIds.Contains(o.Id))
            .Select(o => new RecentOrganizationSummaryDto(
                o.Id,
                o.Name,
                o.Code,
                o.Email,
                o.IsActive,
                _context.Users.Where(u => u.OrganizationId == o.Id && u.RoleType == UserRoleType.OrganizationAdmin && !u.IsDeleted).OrderBy(u => u.CreatedAtUtc).Select(u => u.Email).FirstOrDefault(),
                o.CreatedAtUtc,
                o.LogoUrl
            ))
            .ToListAsync(cancellationToken);

        var recentActivities = await _context.AuditLogs
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

        var stats = new SuperAdminDashboardStatsDto(
            totalOrgs,
            activeOrgs,
            inactiveOrgs,
            totalAdmins,
            totalUsers,
            totalStudents,
            totalTeachers,
            recentCreated,
            recentActiveOrgs,
            recentActivities
        );

        return ApiSuccess(stats);
    }

    // =========================================================================
    // ORGANIZATION ADMIN: PROFILE & SETTINGS
    // =========================================================================

    [HttpGet("my-profile")]
    [Authorize]
    public async Task<IActionResult> GetMyProfile([FromQuery] Guid? organizationId, CancellationToken cancellationToken)
    {
        Guid targetOrgId = CurrentUserService.IsSuperAdmin && organizationId.HasValue && organizationId.Value != Guid.Empty
            ? organizationId.Value
            : (CurrentUserService.OrganizationId ?? Guid.Empty);

        if (targetOrgId == Guid.Empty)
            return ApiError("Tenant context required.", StatusCodes.Status400BadRequest);

        var org = await _context.Organizations
            .Include(o => o.Modules)
            .FirstOrDefaultAsync(o => o.Id == targetOrgId && !o.IsDeleted, cancellationToken);

        if (org == null)
            return ApiError("Organization not found.", StatusCodes.Status404NotFound);

        int activeBranches = await _context.Branches
            .CountAsync(b => b.OrganizationId == targetOrgId && b.IsActive && !b.IsDeleted, cancellationToken);

        var enabledModules = org.Modules
            .Where(m => m.IsEnabled && !m.IsDeleted)
            .Select(m => m.ModuleName)
            .ToList();

        var dto = new OrganizationProfileDto(
            org.Id,
            org.Name,
            org.Code,
            org.LogoUrl,
            org.Email,
            org.Phone,
            org.Address,
            org.ThemePrimaryColor,
            org.ThemeSecondaryColor,
            org.ThemeDarkMode,
            org.MaxBranches,
            activeBranches,
            org.Website,
            org.PrincipalName,
            org.RegistrationNumber,
            org.OrganizationType,
            org.Description,
            org.WorkingDays,
            org.WorkingHours,
            org.TimeZone,
            org.Currency,
            org.DateFormat,
            org.Language,
            enabledModules
        );

        return ApiSuccess(dto);
    }

    [HttpPut("my-profile")]
    [Authorize]
    public async Task<IActionResult> UpdateMyProfile([FromBody] UpdateOrganizationProfileRequest request, [FromQuery] Guid? organizationId, CancellationToken cancellationToken)
    {
        Guid targetOrgId = CurrentUserService.IsSuperAdmin && organizationId.HasValue && organizationId.Value != Guid.Empty
            ? organizationId.Value
            : (CurrentUserService.OrganizationId ?? Guid.Empty);

        if (targetOrgId == Guid.Empty)
            return ApiError("Tenant context required.", StatusCodes.Status400BadRequest);

        var org = await _context.Organizations
            .FirstOrDefaultAsync(o => o.Id == targetOrgId && !o.IsDeleted, cancellationToken);

        if (org == null)
            return ApiError("Organization not found.", StatusCodes.Status404NotFound);

        if (!CurrentUserService.IsSuperAdmin && CurrentUserService.RoleType != UserRoleType.OrganizationAdmin)
            return ApiError("Only Organization Admin or SuperAdmin may update institutional profile.", StatusCodes.Status403Forbidden);

        org.Name = string.IsNullOrWhiteSpace(request.Name) ? org.Name : request.Name.Trim();
        org.LogoUrl = request.LogoUrl?.Trim();
        org.Email = string.IsNullOrWhiteSpace(request.Email) ? org.Email : request.Email.Trim();
        org.Phone = string.IsNullOrWhiteSpace(request.Phone) ? org.Phone : request.Phone.Trim();
        org.Address = string.IsNullOrWhiteSpace(request.Address) ? org.Address : request.Address.Trim();
        org.ThemePrimaryColor = string.IsNullOrWhiteSpace(request.ThemePrimaryColor) ? org.ThemePrimaryColor : request.ThemePrimaryColor.Trim();
        org.ThemeSecondaryColor = string.IsNullOrWhiteSpace(request.ThemeSecondaryColor) ? org.ThemeSecondaryColor : request.ThemeSecondaryColor.Trim();
        org.ThemeDarkMode = request.ThemeDarkMode;
        org.Website = request.Website?.Trim();
        org.PrincipalName = request.PrincipalName?.Trim();
        org.RegistrationNumber = request.RegistrationNumber?.Trim();
        org.OrganizationType = request.OrganizationType?.Trim() ?? org.OrganizationType;
        org.Description = request.Description?.Trim();
        org.WorkingDays = request.WorkingDays?.Trim() ?? org.WorkingDays;
        org.WorkingHours = request.WorkingHours?.Trim() ?? org.WorkingHours;
        org.TimeZone = request.TimeZone?.Trim() ?? org.TimeZone;
        org.Currency = request.Currency?.Trim() ?? org.Currency;
        org.DateFormat = request.DateFormat?.Trim() ?? org.DateFormat;
        org.Language = request.Language?.Trim() ?? org.Language;

        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            AuditAction.Update, "Organization", "Profile", org.Id.ToString(),
            $"Updated organization profile for '{org.Name}'",
            organizationId: org.Id,
            userId: CurrentUserService.UserId,
            userEmail: CurrentUserService.Email,
            cancellationToken: cancellationToken);

        return ApiSuccess("Organization profile updated successfully.");
    }

    [HttpGet("settings")]
    [Authorize]
    public async Task<IActionResult> GetSettings([FromQuery] Guid? organizationId, CancellationToken cancellationToken)
    {
        Guid targetOrgId = CurrentUserService.IsSuperAdmin && organizationId.HasValue && organizationId.Value != Guid.Empty
            ? organizationId.Value
            : (CurrentUserService.OrganizationId ?? Guid.Empty);

        if (targetOrgId == Guid.Empty)
            return ApiError("Tenant context required.", StatusCodes.Status400BadRequest);

        var setting = await _context.OrganizationSettings
            .FirstOrDefaultAsync(s => s.OrganizationId == targetOrgId && !s.IsDeleted, cancellationToken);

        if (setting == null)
        {
            setting = new OrganizationSetting
            {
                Id = Guid.NewGuid(),
                OrganizationId = targetOrgId,
                Currency = "PKR",
                DateFormat = "yyyy-MM-dd",
                TimeFormat = "HH:mm",
                TimeZone = "Pakistan Standard Time",
                Language = "en",
                GradingSystem = "Percentage",
                PassingMarksPercentage = 40.0m,
                WorkingDays = "Monday,Tuesday,Wednesday,Thursday,Friday",
                WorkingHours = "08:00 - 14:00",
                StudentIdFormat = "STU-{yyyy}-{0000}",
                EmployeeIdFormat = "EMP-{yyyy}-{0000}",
                AttendanceThresholdPercentage = 75.0m
            };
            _context.OrganizationSettings.Add(setting);
            await _context.SaveChangesAsync(cancellationToken);
        }

        var dto = new OrganizationSettingDto(
            setting.Id,
            setting.OrganizationId,
            setting.Currency,
            setting.DateFormat,
            setting.TimeFormat,
            setting.TimeZone,
            setting.Language,
            setting.GradingSystem,
            setting.PassingMarksPercentage,
            setting.WorkingDays,
            setting.WorkingHours,
            setting.StudentIdFormat,
            setting.EmployeeIdFormat,
            setting.AttendanceThresholdPercentage
        );

        return ApiSuccess(dto);
    }

    [HttpPut("settings")]
    [Authorize]
    public async Task<IActionResult> UpdateSettings([FromBody] UpdateOrganizationSettingRequest request, [FromQuery] Guid? organizationId, CancellationToken cancellationToken)
    {
        Guid targetOrgId = CurrentUserService.IsSuperAdmin && organizationId.HasValue && organizationId.Value != Guid.Empty
            ? organizationId.Value
            : (CurrentUserService.OrganizationId ?? Guid.Empty);

        if (targetOrgId == Guid.Empty)
            return ApiError("Tenant context required.", StatusCodes.Status400BadRequest);

        if (!CurrentUserService.IsSuperAdmin && CurrentUserService.RoleType != UserRoleType.OrganizationAdmin)
            return ApiError("Only Organization Admin or SuperAdmin may update institutional settings.", StatusCodes.Status403Forbidden);

        var setting = await _context.OrganizationSettings
            .FirstOrDefaultAsync(s => s.OrganizationId == targetOrgId && !s.IsDeleted, cancellationToken);

        if (setting == null)
        {
            setting = new OrganizationSetting { Id = Guid.NewGuid(), OrganizationId = targetOrgId };
            _context.OrganizationSettings.Add(setting);
        }

        setting.Currency = string.IsNullOrWhiteSpace(request.Currency) ? "PKR" : request.Currency.Trim();
        setting.DateFormat = string.IsNullOrWhiteSpace(request.DateFormat) ? "yyyy-MM-dd" : request.DateFormat.Trim();
        setting.TimeFormat = string.IsNullOrWhiteSpace(request.TimeFormat) ? "HH:mm" : request.TimeFormat.Trim();
        setting.TimeZone = string.IsNullOrWhiteSpace(request.TimeZone) ? "Pakistan Standard Time" : request.TimeZone.Trim();
        setting.Language = string.IsNullOrWhiteSpace(request.Language) ? "en" : request.Language.Trim();
        setting.GradingSystem = string.IsNullOrWhiteSpace(request.GradingSystem) ? "Percentage" : request.GradingSystem.Trim();
        setting.PassingMarksPercentage = request.PassingMarksPercentage.HasValue && request.PassingMarksPercentage.Value > 0 ? request.PassingMarksPercentage.Value : 40.0m;
        setting.WorkingDays = string.IsNullOrWhiteSpace(request.WorkingDays) ? "Monday,Tuesday,Wednesday,Thursday,Friday" : request.WorkingDays.Trim();
        setting.WorkingHours = string.IsNullOrWhiteSpace(request.WorkingHours) ? "08:00 - 14:00" : request.WorkingHours.Trim();
        setting.StudentIdFormat = string.IsNullOrWhiteSpace(request.StudentIdFormat) ? "STU-{yyyy}-{0000}" : request.StudentIdFormat.Trim();
        setting.EmployeeIdFormat = string.IsNullOrWhiteSpace(request.EmployeeIdFormat) ? "EMP-{yyyy}-{0000}" : request.EmployeeIdFormat.Trim();
        setting.AttendanceThresholdPercentage = request.AttendanceThresholdPercentage.HasValue && request.AttendanceThresholdPercentage.Value > 0 ? request.AttendanceThresholdPercentage.Value : 75.0m;

        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            AuditAction.Update, "Organization", "Settings", setting.Id.ToString(),
            "Updated organization operational rules and formatting settings",
            organizationId: targetOrgId,
            userId: CurrentUserService.UserId,
            userEmail: CurrentUserService.Email,
            cancellationToken: cancellationToken);

        return ApiSuccess("Organization settings updated successfully.");
    }

    [HttpGet("notifications")]
    [Authorize]
    public async Task<IActionResult> GetNotificationSettings([FromQuery] Guid? organizationId, CancellationToken cancellationToken)
    {
        Guid targetOrgId = CurrentUserService.IsSuperAdmin && organizationId.HasValue && organizationId.Value != Guid.Empty
            ? organizationId.Value
            : (CurrentUserService.OrganizationId ?? Guid.Empty);

        if (targetOrgId == Guid.Empty)
            return ApiError("Tenant context required.", StatusCodes.Status400BadRequest);

        var notif = await _context.NotificationSettings
            .FirstOrDefaultAsync(n => n.OrganizationId == targetOrgId && !n.IsDeleted, cancellationToken);

        if (notif == null)
        {
            notif = new NotificationSetting
            {
                Id = Guid.NewGuid(),
                OrganizationId = targetOrgId,
                EmailEnabled = true,
                PushEnabled = true,
                NotifyOnAdmission = true,
                NotifyOnFeeDue = true,
                NotifyOnFeePayment = true,
                NotifyOnAttendanceAbsence = true,
                NotifyOnExamResult = true,
                NotifyOnAnnouncement = true
            };
            _context.NotificationSettings.Add(notif);
            await _context.SaveChangesAsync(cancellationToken);
        }

        var dto = new NotificationSettingDto(
            notif.Id,
            notif.OrganizationId,
            notif.EmailEnabled,
            notif.SmsEnabled,
            notif.WhatsAppEnabled,
            notif.PushEnabled,
            notif.NotifyOnAdmission,
            notif.NotifyOnFeeDue,
            notif.NotifyOnFeePayment,
            notif.NotifyOnAttendanceAbsence,
            notif.NotifyOnExamResult,
            notif.NotifyOnAnnouncement
        );

        return ApiSuccess(dto);
    }

    [HttpPut("notifications")]
    [Authorize]
    public async Task<IActionResult> UpdateNotificationSettings([FromBody] UpdateNotificationSettingRequest request, [FromQuery] Guid? organizationId, CancellationToken cancellationToken)
    {
        Guid targetOrgId = CurrentUserService.IsSuperAdmin && organizationId.HasValue && organizationId.Value != Guid.Empty
            ? organizationId.Value
            : (CurrentUserService.OrganizationId ?? Guid.Empty);

        if (targetOrgId == Guid.Empty)
            return ApiError("Tenant context required.", StatusCodes.Status400BadRequest);

        if (!CurrentUserService.IsSuperAdmin && CurrentUserService.RoleType != UserRoleType.OrganizationAdmin)
            return ApiError("Only Organization Admin or SuperAdmin may update notification preferences.", StatusCodes.Status403Forbidden);

        var notif = await _context.NotificationSettings
            .FirstOrDefaultAsync(n => n.OrganizationId == targetOrgId && !n.IsDeleted, cancellationToken);

        if (notif == null)
        {
            notif = new NotificationSetting { Id = Guid.NewGuid(), OrganizationId = targetOrgId };
            _context.NotificationSettings.Add(notif);
        }

        notif.EmailEnabled = request.EmailEnabled;
        notif.SmsEnabled = request.SmsEnabled;
        notif.WhatsAppEnabled = request.WhatsAppEnabled;
        notif.PushEnabled = request.PushEnabled;
        notif.NotifyOnAdmission = request.NotifyOnAdmission;
        notif.NotifyOnFeeDue = request.NotifyOnFeeDue;
        notif.NotifyOnFeePayment = request.NotifyOnFeePayment;
        notif.NotifyOnAttendanceAbsence = request.NotifyOnAttendanceAbsence;
        notif.NotifyOnExamResult = request.NotifyOnExamResult;
        notif.NotifyOnAnnouncement = request.NotifyOnAnnouncement;

        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            AuditAction.Update, "Organization", "NotificationSettings", notif.Id.ToString(),
            "Updated organization notification and automated alert preferences",
            organizationId: targetOrgId,
            userId: CurrentUserService.UserId,
            userEmail: CurrentUserService.Email,
            cancellationToken: cancellationToken);

        return ApiSuccess("Notification settings updated successfully.");
    }
}
