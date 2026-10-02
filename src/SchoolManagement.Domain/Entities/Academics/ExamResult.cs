using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Entities.Security;
using SchoolManagement.Domain.Entities.Student;
using SchoolManagement.Domain.Entities.Tenant;

namespace SchoolManagement.Domain.Entities.Academics;

public class ExamResult : BaseEntity, ITenantEntity
{
    public Guid OrganizationId { get; set; }
    public Guid? BranchId { get; set; }
    public Guid ExamScheduleId { get; set; }
    public Guid StudentId { get; set; }
    public decimal MarksObtained { get; set; }
    public string? Grade { get; set; } // A+, A, B, C, D, F
    public decimal Percentage { get; set; }
    public decimal? Gpa { get; set; }
    public string? Remarks { get; set; }
    public Guid? EnteredByUserId { get; set; }
    public bool IsFinalized { get; set; } = false;

    public Organization? Organization { get; set; }
    public ExamSchedule? ExamSchedule { get; set; }
    public Student.Student? Student { get; set; }
    public User? EnteredByUser { get; set; }
}
