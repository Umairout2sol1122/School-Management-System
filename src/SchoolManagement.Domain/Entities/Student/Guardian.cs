using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Entities.Security;
using SchoolManagement.Domain.Entities.Tenant;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Domain.Entities.Student;

public class Guardian : BaseEntity, ITenantEntity
{
    public Guid OrganizationId { get; set; }
    public Guid? BranchId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public GuardianRelationshipType RelationshipType { get; set; } = GuardianRelationshipType.Father;
    public string? NationalId { get; set; }
    public string? Email { get; set; }
    public string Phone { get; set; } = string.Empty;
    public string? AlternatePhone { get; set; }
    public string? Occupation { get; set; }
    public decimal? AnnualIncome { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public bool IsEmergencyContact { get; set; } = true;
    public Guid? UserId { get; set; }

    public Organization? Organization { get; set; }
    public Branch? Branch { get; set; }
    public User? User { get; set; }
    public ICollection<StudentGuardian> StudentGuardians { get; set; } = new List<StudentGuardian>();

    public string FullName => $"{FirstName} {LastName}".Trim();
}
