using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Entities.Student;

public class StudentGuardian : BaseEntity, ITenantEntity
{
    public Guid OrganizationId { get; set; }
    public Guid? BranchId { get; set; }
    public Guid StudentId { get; set; }
    public Guid GuardianId { get; set; }
    public string Relationship { get; set; } = string.Empty;
    public bool IsPrimaryContact { get; set; } = true;
    public bool IsAuthorizedToPickup { get; set; } = true;

    public Student? Student { get; set; }
    public Guardian? Guardian { get; set; }
}
