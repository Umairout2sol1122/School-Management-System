using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Entities.Security;
using SchoolManagement.Domain.Entities.Tenant;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Domain.Entities.Student;

public class Student : BaseEntity, ITenantEntity
{
    public Guid OrganizationId { get; set; }
    public Guid? BranchId { get; set; }
    public string AdmissionNumber { get; set; } = string.Empty;
    public string? RollNumber { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateTime DateOfBirth { get; set; }
    public Gender Gender { get; set; } = Gender.Male;
    public string? BloodGroup { get; set; }
    public string? NationalIdOrBForm { get; set; }
    public DateTime AdmissionDate { get; set; } = DateTime.UtcNow;
    public StudentStatus Status { get; set; } = StudentStatus.Active;
    public string CurrentGrade { get; set; } = string.Empty;
    public string? Section { get; set; }
    public string? EmergencyContactName { get; set; }
    public string? EmergencyContactPhone { get; set; }
    public string? EmergencyContactRelationship { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? MedicalNotes { get; set; }
    public string? ProfilePictureUrl { get; set; }
    public Guid? UserId { get; set; }

    public Organization? Organization { get; set; }
    public Branch? Branch { get; set; }
    public User? User { get; set; }
    public ICollection<StudentGuardian> StudentGuardians { get; set; } = new List<StudentGuardian>();

    public string FullName => $"{FirstName} {LastName}".Trim();
}
