using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Entities.Tenant;

public class Announcement : BaseEntity, ITenantEntity
{
    public Guid OrganizationId { get; set; }
    public Guid? BranchId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string TargetAudience { get; set; } = "All"; // "All", "Teachers", "Students", "Parents", "Staff", "Class", "Department"
    public Guid? ClassGradeId { get; set; }
    public Guid? DepartmentId { get; set; }
    public string Priority { get; set; } = "Normal"; // "Normal", "Important", "Urgent"
    public DateTime PublishDate { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiryDate { get; set; }
    public bool IsActive { get; set; } = true;

    public Organization? Organization { get; set; }
    public Branch? Branch { get; set; }
    public ClassGrade? ClassGrade { get; set; }
    public Department? Department { get; set; }
}
