using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Entities.Tenant;

public class AcademicSession : BaseEntity, ITenantEntity
{
    public Guid OrganizationId { get; set; }
    public Guid? BranchId { get; set; }
    public string Name { get; set; } = string.Empty; // e.g. "2026-2027", "Batch Jan-2026", "Spring 2026"
    public string Code { get; set; } = string.Empty;
    public string Type { get; set; } = "Annual"; // "Annual", "Semester", "Term", "Batch"
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsCurrent { get; set; } = false;
    public bool IsActive { get; set; } = true;

    public Organization? Organization { get; set; }
    public Branch? Branch { get; set; }
}
