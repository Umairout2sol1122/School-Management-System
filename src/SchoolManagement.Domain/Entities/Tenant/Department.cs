using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Entities.Security;

namespace SchoolManagement.Domain.Entities.Tenant;

public class Department : BaseEntity, ITenantEntity
{
    public Guid OrganizationId { get; set; }
    public Guid? BranchId { get; set; }
    public string Name { get; set; } = string.Empty; // e.g. "Science", "Computer Science", "Administration"
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? HeadOfDepartmentUserId { get; set; }
    public bool IsActive { get; set; } = true;

    public Organization? Organization { get; set; }
    public Branch? Branch { get; set; }
    public User? HeadOfDepartmentUser { get; set; }
}
