namespace SchoolManagement.Application.Common.Models;

public record RoleDto(
    Guid Id,
    string Name,
    string? Description,
    bool IsSystemRole,
    int PermissionsCount
);

public record RoleDetailsDto(
    Guid Id,
    string Name,
    string? Description,
    bool IsSystemRole,
    IReadOnlyList<PermissionDto> Permissions
);

public record CreateRoleRequestDto(
    string Name,
    string? Description,
    IReadOnlyList<Guid> PermissionIds
);

public record UpdateRoleRequestDto(
    string Name,
    string? Description,
    IReadOnlyList<Guid> PermissionIds
);

public record PermissionDto(
    Guid Id,
    string Code,
    string Name,
    string Module,
    string? Description
);

public record PermissionGroupDto(
    string Module,
    IReadOnlyList<PermissionDto> Permissions
);
