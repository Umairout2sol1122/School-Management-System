using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Entities.Tenant;

public class SubjectCourse : BaseEntity, ITenantEntity
{
    public Guid OrganizationId { get; set; }
    public Guid? BranchId { get; set; }
    public Guid? DepartmentId { get; set; }
    public Guid? ClassGradeId { get; set; }
    public string Name { get; set; } = string.Empty; // e.g. "Mathematics", "Graphic Design", "Data Structures"
    public string Code { get; set; } = string.Empty;
    public int CreditHours { get; set; } = 3;
    public string Type { get; set; } = "Core"; // "Core", "Elective", "Practical"
    public bool IsActive { get; set; } = true;

    public Organization? Organization { get; set; }
    public Branch? Branch { get; set; }
    public Department? Department { get; set; }
    public ClassGrade? ClassGrade { get; set; }
}
