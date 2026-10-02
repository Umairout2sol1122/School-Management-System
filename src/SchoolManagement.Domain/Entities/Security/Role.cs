using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Entities.Security;

public class Role : BaseEntity
{
    public Guid? OrganizationId { get; set; } // Null for system roles, populated for org-custom roles
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsSystemRole { get; set; } = false;

    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}
