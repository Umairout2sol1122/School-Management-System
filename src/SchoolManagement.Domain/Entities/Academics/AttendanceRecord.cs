using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Entities.Staff;
using SchoolManagement.Domain.Entities.Student;
using SchoolManagement.Domain.Entities.Tenant;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Domain.Entities.Academics;

public class AttendanceRecord : BaseEntity, ITenantEntity
{
    public Guid OrganizationId { get; set; }
    public Guid? BranchId { get; set; }
    public Guid ClassGradeId { get; set; }
    public Guid? ClassSectionId { get; set; }
    public Guid? SubjectCourseId { get; set; }
    public Guid StudentId { get; set; }
    public DateTime Date { get; set; }
    public AttendanceStatus Status { get; set; } = AttendanceStatus.Present;
    public string? Remarks { get; set; }
    public Guid? MarkedByStaffId { get; set; }
    public bool IsLocked { get; set; } = false;

    public Organization? Organization { get; set; }
    public Branch? Branch { get; set; }
    public ClassGrade? ClassGrade { get; set; }
    public ClassSection? ClassSection { get; set; }
    public SubjectCourse? SubjectCourse { get; set; }
    public Student.Student? Student { get; set; }
    public StaffProfile? MarkedByStaff { get; set; }
}
