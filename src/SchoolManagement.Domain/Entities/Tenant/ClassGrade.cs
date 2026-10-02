using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Entities.Tenant;

public class ClassGrade : BaseEntity, ITenantEntity
{
    public Guid OrganizationId { get; set; }
    public Guid? BranchId { get; set; }
    public Guid? DepartmentId { get; set; }
    public string Name { get; set; } = string.Empty; // e.g. "Grade 1", "Playgroup", "Diploma Web Development"
    public string Code { get; set; } = string.Empty;
    public int DisplayOrder { get; set; } = 0;
    public int Capacity { get; set; } = 40;
    public bool IsActive { get; set; } = true;

    public Organization? Organization { get; set; }
    public Branch? Branch { get; set; }
    public Department? Department { get; set; }
    public ICollection<ClassSection> Sections { get; set; } = new List<ClassSection>();
}
