using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Entities.Security;
using SchoolManagement.Domain.Entities.Tenant;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Domain.Entities.Student;

public class AdmissionApplication : BaseEntity, ITenantEntity
{
    public Guid OrganizationId { get; set; }
    public Guid? BranchId { get; set; }
    public string ApplicationNumber { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateTime DateOfBirth { get; set; }
    public Gender Gender { get; set; } = Gender.Male;
    public string DesiredGrade { get; set; } = string.Empty;
    public string? GuardianName { get; set; }
    public string? GuardianPhone { get; set; }
    public string? GuardianEmail { get; set; }
    public string? GuardianRelationship { get; set; }
    public string? Address { get; set; }
    public ApplicationStatus Status { get; set; } = ApplicationStatus.Pending;
    public string? ReviewNotes { get; set; }
    public string? RejectionReason { get; set; }
    public Guid? EnrolledStudentId { get; set; }
    public DateTime? DecisionDateUtc { get; set; }
    public Guid? ReviewedByUserId { get; set; }

    public Organization? Organization { get; set; }
    public Branch? Branch { get; set; }
    public Student? EnrolledStudent { get; set; }
    public User? ReviewedByUser { get; set; }
}
