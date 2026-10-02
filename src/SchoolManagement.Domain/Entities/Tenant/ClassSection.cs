using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Entities.Security;

namespace SchoolManagement.Domain.Entities.Tenant;

public class ClassSection : BaseEntity, ITenantEntity
{
    public Guid OrganizationId { get; set; }
    public Guid ClassGradeId { get; set; }
    public Guid? BranchId { get; set; }
    public string Name { get; set; } = string.Empty; // e.g. "Section A", "Morning Batch"
    public string? RoomNumber { get; set; }
    public int Capacity { get; set; } = 35;
    public Guid? ClassTeacherUserId { get; set; }
    public bool IsActive { get; set; } = true;

    public Organization? Organization { get; set; }
    public ClassGrade? ClassGrade { get; set; }
    public Branch? Branch { get; set; }
    public User? ClassTeacherUser { get; set; }
}
