using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Entities.Tenant;

public class OrganizationModule : BaseEntity, ITenantEntity
{
    public Guid OrganizationId { get; set; }
    public Guid? BranchId { get; set; }
    public string ModuleName { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;

    public Organization? Organization { get; set; }
}
