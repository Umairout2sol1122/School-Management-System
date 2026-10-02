using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Entities.Security;

namespace SchoolManagement.Domain.Entities.Tenant;

public class CourseAssignment : BaseEntity, ITenantEntity
{
    public Guid OrganizationId { get; set; }
    public Guid? BranchId { get; set; }
    public Guid ClassSectionId { get; set; }
    public Guid SubjectCourseId { get; set; }
    public Guid TeacherUserId { get; set; }
    public Guid? AcademicSessionId { get; set; }
    public bool IsActive { get; set; } = true;

    public Organization? Organization { get; set; }
    public Branch? Branch { get; set; }
    public ClassSection? ClassSection { get; set; }
    public SubjectCourse? SubjectCourse { get; set; }
    public User? TeacherUser { get; set; }
    public AcademicSession? AcademicSession { get; set; }
}
