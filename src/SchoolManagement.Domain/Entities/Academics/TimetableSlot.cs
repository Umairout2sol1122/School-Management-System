using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Entities.Staff;
using SchoolManagement.Domain.Entities.Tenant;

namespace SchoolManagement.Domain.Entities.Academics;

public class TimetableSlot : BaseEntity, ITenantEntity
{
    public Guid OrganizationId { get; set; }
    public Guid? BranchId { get; set; }
    public Guid? AcademicSessionId { get; set; }
    public Guid ClassGradeId { get; set; }
    public Guid? ClassSectionId { get; set; }
    public Guid SubjectCourseId { get; set; }
    public Guid? TeacherStaffId { get; set; }
    public Guid? SubstituteTeacherStaffId { get; set; }
    public string? SubstitutionNotes { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public string? RoomNumber { get; set; }
    public bool IsActive { get; set; } = true;

    public Organization? Organization { get; set; }
    public Branch? Branch { get; set; }
    public AcademicSession? AcademicSession { get; set; }
    public ClassGrade? ClassGrade { get; set; }
    public ClassSection? ClassSection { get; set; }
    public SubjectCourse? SubjectCourse { get; set; }
    public StaffProfile? TeacherStaff { get; set; }
    public StaffProfile? SubstituteTeacherStaff { get; set; }
}
