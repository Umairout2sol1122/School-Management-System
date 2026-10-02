using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Entities.Security;
using SchoolManagement.Domain.Entities.Student;
using SchoolManagement.Domain.Entities.Tenant;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Domain.Entities.Academics;

public class AssignmentSubmission : BaseEntity, ITenantEntity
{
    public Guid OrganizationId { get; set; }
    public Guid? BranchId { get; set; }
    public Guid HomeworkAssignmentId { get; set; }
    public Guid StudentId { get; set; }
    public DateTime SubmissionDate { get; set; } = DateTime.UtcNow;
    public string? Content { get; set; }
    public string? AttachmentUrl { get; set; }
    public string? AttachmentFileName { get; set; }
    public SubmissionStatus Status { get; set; } = SubmissionStatus.Submitted;
    public decimal? MarksAwarded { get; set; }
    public string? Feedback { get; set; }
    public Guid? GradedByUserId { get; set; }
    public DateTime? GradedAtUtc { get; set; }

    public Organization? Organization { get; set; }
    public HomeworkAssignment? HomeworkAssignment { get; set; }
    public Student.Student? Student { get; set; }
    public User? GradedByUser { get; set; }
}
