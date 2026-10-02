using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Application.Common.Interfaces;

public interface ICurrentUserService
{
    Guid? UserId { get; }
    string? Email { get; }
    Guid? OrganizationId { get; }
    Guid? BranchId { get; }
    UserRoleType? RoleType { get; }
    bool IsSuperAdmin { get; }
    bool IsAuthenticated { get; }
    IEnumerable<string> Permissions { get; }

    bool HasPermission(string permission);
}
