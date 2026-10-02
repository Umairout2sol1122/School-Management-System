using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Entities.Tenant;

namespace SchoolManagement.Domain.Entities.Academics;

public class ExamSchedule : BaseEntity, ITenantEntity
{
    public Guid OrganizationId { get; set; }
    public Guid? BranchId { get; set; }
    public Guid? AcademicSessionId { get; set; }
    public Guid ClassGradeId { get; set; }
    public Guid? ClassSectionId { get; set; }
    public Guid SubjectCourseId { get; set; }
    public string Title { get; set; } = string.Empty; // e.g. "Midterm Examination 2026"
    public string Term { get; set; } = "Midterm"; // Midterm, Final, Quiz, Unit Test
    public DateTime ExamDate { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public string? RoomNumber { get; set; }
    public decimal TotalMarks { get; set; } = 100.0m;
    public decimal PassingMarks { get; set; } = 40.0m;
    public bool IsPublished { get; set; } = false;
    public bool IsLocked { get; set; } = false;

    public Organization? Organization { get; set; }
    public Branch? Branch { get; set; }
    public AcademicSession? AcademicSession { get; set; }
    public ClassGrade? ClassGrade { get; set; }
    public ClassSection? ClassSection { get; set; }
    public SubjectCourse? SubjectCourse { get; set; }
    public ICollection<ExamResult> Results { get; set; } = new List<ExamResult>();
}
