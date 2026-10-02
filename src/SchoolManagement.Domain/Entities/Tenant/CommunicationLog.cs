using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Entities.Security;

namespace SchoolManagement.Domain.Entities.Tenant;

public class CommunicationLog : BaseEntity, ITenantEntity
{
    public Guid OrganizationId { get; set; }
    public Guid? BranchId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Channel { get; set; } = "InApp"; // InApp, Email, SMS, WhatsApp
    public string TargetAudience { get; set; } = "All"; // All, Teachers, Students, Parents, Staff
    public int RecipientCount { get; set; } = 0;
    public string Status { get; set; } = "Delivered"; // Sent, Delivered, Failed
    public string? FailureReason { get; set; }
    public Guid? SentByUserId { get; set; }
    public string? SentByUserEmail { get; set; }

    public Organization? Organization { get; set; }
    public Branch? Branch { get; set; }
}
