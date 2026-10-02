namespace SchoolManagement.Domain.Common;

public interface ITenantEntity
{
    Guid OrganizationId { get; set; }
    Guid? BranchId { get; set; }
}
