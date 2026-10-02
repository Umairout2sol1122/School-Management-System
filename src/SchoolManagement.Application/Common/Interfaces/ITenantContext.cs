namespace SchoolManagement.Application.Common.Interfaces;

public interface ITenantContext
{
    Guid? OrganizationId { get; }
    Guid? BranchId { get; }
    bool IsSuperAdmin { get; }

    void SetTenant(Guid? organizationId, Guid? branchId, bool isSuperAdmin = false);
}
