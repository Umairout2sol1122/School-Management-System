using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Application.Common.Models;

public record UserDto(
    Guid Id,
    Guid OrganizationId,
    Guid? BranchId,
    string Username,
    string Email,
    string FirstName,
    string LastName,
    string FullName,
    UserRoleType RoleType,
    bool IsActive,
    IReadOnlyList<string> Permissions,
    string? OrganizationName = null,
    string? OrganizationLogoUrl = null,
    string? ThemePrimaryColor = null,
    string? ThemeSecondaryColor = null,
    string? BranchName = null,
    IReadOnlyList<string>? EnabledModules = null
);
