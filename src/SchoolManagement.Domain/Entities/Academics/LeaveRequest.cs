using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Entities.Security;
using SchoolManagement.Domain.Entities.Staff;
using SchoolManagement.Domain.Entities.Student;
using SchoolManagement.Domain.Entities.Tenant;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Domain.Entities.Academics;

public class LeaveRequest : BaseEntity, ITenantEntity
{
    public Guid OrganizationId { get; set; }
    public Guid? BranchId { get; set; }
    public Guid RequesterUserId { get; set; }
    public RequesterType RequesterType { get; set; } = RequesterType.Staff;
    public Guid? StaffProfileId { get; set; }
    public Guid? StudentId { get; set; }
    public string LeaveType { get; set; } = "Casual"; // Casual, Sick, Medical, Emergency, Annual
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? AttachmentUrl { get; set; }
    public LeaveStatus Status { get; set; } = LeaveStatus.Pending;
    public Guid? ApprovedByUserId { get; set; }
    public string? DecisionRemarks { get; set; }
    public DateTime? DecisionDateUtc { get; set; }

    public Organization? Organization { get; set; }
    public Branch? Branch { get; set; }
    public User? RequesterUser { get; set; }
    public StaffProfile? StaffProfile { get; set; }
    public Student.Student? Student { get; set; }
    public User? ApprovedByUser { get; set; }
}
