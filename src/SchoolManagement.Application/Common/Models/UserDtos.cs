using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Application.Common.Models;

public record CreateUserRequestDto(
    Guid OrganizationId,
    Guid? BranchId,
    string Username,
    string Email,
    string FirstName,
    string LastName,
    string Password,
    UserRoleType RoleType,
    IReadOnlyList<Guid>? RoleIds,
    string? PhoneNumber
);

public record UpdateUserRequestDto(
    string FirstName,
    string LastName,
    Guid? BranchId,
    UserRoleType RoleType,
    IReadOnlyList<Guid>? RoleIds,
    string? PhoneNumber
);

public record UserSummaryDto(
    Guid Id,
    Guid OrganizationId,
    string? OrganizationName,
    Guid? BranchId,
    string? BranchName,
    string Username,
    string Email,
    string FullName,
    UserRoleType RoleType,
    bool IsActive,
    bool IsLocked,
    DateTime? LastLoginAtUtc
);

public record UserDetailsDto(
    Guid Id,
    Guid OrganizationId,
    string? OrganizationName,
    Guid? BranchId,
    string? BranchName,
    string Username,
    string Email,
    string FirstName,
    string LastName,
    string FullName,
    string? PhoneNumber,
    UserRoleType RoleType,
    bool IsActive,
    bool IsLocked,
    DateTime? LockoutEndUtc,
    DateTime? LastLoginAtUtc,
    IReadOnlyList<RoleDto> Roles,
    IReadOnlyList<string> Permissions
);

public record AdminResetPasswordRequestDto(
    string NewPassword
);
