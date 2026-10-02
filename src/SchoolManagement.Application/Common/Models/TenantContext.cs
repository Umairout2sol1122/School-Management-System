using SchoolManagement.Application.Common.Interfaces;

namespace SchoolManagement.Application.Common.Models;

public class TenantContext : ITenantContext
{
    public Guid? OrganizationId { get; private set; }
    public Guid? BranchId { get; private set; }
    public bool IsSuperAdmin { get; private set; }

    public void SetTenant(Guid? organizationId, Guid? branchId, bool isSuperAdmin = false)
    {
        OrganizationId = organizationId;
        BranchId = branchId;
        IsSuperAdmin = isSuperAdmin;
    }
}
