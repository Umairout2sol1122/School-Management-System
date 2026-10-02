using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Entities.Security;
using SchoolManagement.Domain.Entities.Student;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Domain.Entities.Tenant;

public class Certificate : BaseEntity, ITenantEntity
{
    public Guid OrganizationId { get; set; }
    public Guid? BranchId { get; set; }
    public Guid StudentId { get; set; }
    public CertificateType CertificateType { get; set; }
    public string SerialNumber { get; set; } = string.Empty;
    public CertificateStatus Status { get; set; } = CertificateStatus.Issued;
    public DateTime IssueDate { get; set; } = DateTime.UtcNow;
    public Guid? IssuedByUserId { get; set; }
    public string? IssuedByName { get; set; }
    public string VerificationCode { get; set; } = string.Empty;
    public string? Conduct { get; set; }
    public string? LeavingReason { get; set; }
    public string? PromotedToClass { get; set; }
    public string? Remarks { get; set; }
    public string? RevocationReason { get; set; }
    public DateTime? RevokedAt { get; set; }
    public Guid? RevokedByUserId { get; set; }

    // Navigation properties
    public Organization? Organization { get; set; }
    public Branch? Branch { get; set; }
    public Student.Student? Student { get; set; }
    public User? IssuedByUser { get; set; }
}
