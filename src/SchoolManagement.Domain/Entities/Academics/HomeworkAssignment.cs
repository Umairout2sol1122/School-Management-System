using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Entities.Security;
using SchoolManagement.Domain.Entities.Tenant;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Domain.Entities.Academics;

public class HomeworkAssignment : BaseEntity, ITenantEntity
{
    public Guid OrganizationId { get; set; }
    public Guid? BranchId { get; set; }
    public Guid ClassGradeId { get; set; }
    public Guid? ClassSectionId { get; set; }
    public Guid SubjectCourseId { get; set; }
    public Guid TeacherUserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime DueDate { get; set; }
    public int MaxMarks { get; set; } = 100;
    public string? AttachmentUrl { get; set; }
    public string? AttachmentFileName { get; set; }
    public AssignmentStatus Status { get; set; } = AssignmentStatus.Active;

    public Organization? Organization { get; set; }
    public Branch? Branch { get; set; }
    public ClassGrade? ClassGrade { get; set; }
    public ClassSection? ClassSection { get; set; }
    public SubjectCourse? SubjectCourse { get; set; }
    public User? TeacherUser { get; set; }
    public ICollection<AssignmentSubmission> Submissions { get; set; } = new List<AssignmentSubmission>();
}
