namespace SchoolManagement.Application.Common.Models;

public record CreateOrganizationRequestDto(
    string Name,
    string Code,
    string Email,
    string Phone,
    string Address,
    string? LogoUrl,
    string? ThemePrimaryColor,
    string? ThemeSecondaryColor,
    string InitialAdminEmail,
    string? InitialAdminUsername,
    string InitialAdminPassword,
    string InitialAdminFirstName,
    string InitialAdminLastName,
    int? MaxBranches = 5
);

public record UpdateOrganizationRequestDto(
    string Name,
    string Email,
    string Phone,
    string Address,
    string? LogoUrl,
    string? ThemePrimaryColor,
    string? ThemeSecondaryColor,
    bool ThemeDarkMode,
    int? MaxBranches = null
);

public record OrganizationSummaryDto(
    Guid Id,
    string Name,
    string Code,
    string Email,
    string Phone,
    bool IsActive,
    int BranchesCount,
    int UsersCount,
    string ThemePrimaryColor,
    string ThemeSecondaryColor,
    DateTime CreatedAtUtc,
    string? AdminName = null,
    string? AdminEmail = null,
    Guid? AdminId = null,
    int EnabledModulesCount = 0,
    string? LogoUrl = null,
    int MaxBranches = 5
);

public record AdminUserSummaryDto(
    Guid Id,
    string FullName,
    string Email,
    string Username,
    string? Phone,
    bool IsActive,
    DateTime? LastLoginAtUtc
);

public record OrganizationModuleItemDto(
    string ModuleName,
    string DisplayName,
    string Description,
    bool IsEnabled
);

public record ModuleToggleDto(
    string ModuleName,
    bool IsEnabled
);

public record UpdateOrganizationModulesRequestDto(
    List<ModuleToggleDto> Modules
);

public record ResetAdminPasswordRequestDto(
    string NewPassword
);

public record RecentOrganizationSummaryDto(
    Guid Id,
    string Name,
    string Code,
    string? Email,
    bool IsActive,
    string? AdminEmail,
    DateTime CreatedAtUtc,
    string? LogoUrl
);

public record SuperAdminDashboardStatsDto(
    int TotalOrganizations,
    int ActiveOrganizations,
    int InactiveOrganizations,
    int TotalOrganizationAdmins,
    int TotalUsers,
    int TotalStudents,
    int TotalTeachers,
    IReadOnlyList<RecentOrganizationSummaryDto> RecentlyCreatedOrganizations,
    IReadOnlyList<RecentOrganizationSummaryDto> RecentlyActiveOrganizations,
    IReadOnlyList<RecentActivityDto> RecentActivities
);

public record OrganizationDetailsDto(
    Guid Id,
    string Name,
    string Code,
    string Email,
    string Phone,
    string Address,
    string? LogoUrl,
    string ThemePrimaryColor,
    string ThemeSecondaryColor,
    bool ThemeDarkMode,
    bool IsActive,
    DateTime CreatedAtUtc,
    IReadOnlyList<BranchDto> Branches,
    AdminUserSummaryDto? AdminUser = null,
    IReadOnlyList<OrganizationModuleItemDto>? Modules = null,
    int StudentsCount = 0,
    int StaffCount = 0,
    int TotalUsersCount = 0,
    DateTime? UpdatedAtUtc = null,
    int MaxBranches = 5
);

public record SchoolProfileDto(
    Guid Id,
    string Name,
    string Code,
    string Email,
    string Phone,
    string Address,
    string? LogoUrl,
    string ThemePrimaryColor,
    string ThemeSecondaryColor,
    bool ThemeDarkMode,
    bool IsActive,
    int BranchesCount,
    int UsersCount,
    DateTime CreatedAtUtc,
    int MaxBranches = 5
);

public record UpdateSchoolProfileRequestDto(
    string Name,
    string? Email,
    string? Phone,
    string? Address,
    string? LogoUrl,
    string? ThemePrimaryColor,
    string? ThemeSecondaryColor,
    bool ThemeDarkMode
);

