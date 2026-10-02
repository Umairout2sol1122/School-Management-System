using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Entities.Tenant;

public class NotificationSetting : BaseEntity, ITenantEntity
{
    public Guid OrganizationId { get; set; }
    public Guid? BranchId { get; set; }
    public bool EmailEnabled { get; set; } = true;
    public bool SmsEnabled { get; set; } = false;
    public bool WhatsAppEnabled { get; set; } = false;
    public bool PushEnabled { get; set; } = true;

    // Event triggers
    public bool NotifyOnAdmission { get; set; } = true;
    public bool NotifyOnFeeDue { get; set; } = true;
    public bool NotifyOnFeePayment { get; set; } = true;
    public bool NotifyOnAttendanceAbsence { get; set; } = true;
    public bool NotifyOnExamResult { get; set; } = true;
    public bool NotifyOnAnnouncement { get; set; } = true;

    public Organization? Organization { get; set; }
}
