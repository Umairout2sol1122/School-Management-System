using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Domain.Entities.Audit;

public class AuditLog : BaseEntity
{
    public Guid? OrganizationId { get; set; }
    public Guid? BranchId { get; set; }
    public Guid? UserId { get; set; }
    public string UserEmail { get; set; } = "Anonymous";
    public AuditAction Action { get; set; }
    public string Module { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public string Details { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public string? CorrelationId { get; set; }
    public bool IsOfflineSync { get; set; } = false;
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
}
