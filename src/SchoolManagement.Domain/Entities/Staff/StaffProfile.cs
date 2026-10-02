using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Entities.Security;
using SchoolManagement.Domain.Entities.Tenant;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Domain.Entities.Staff;

public class StaffProfile : BaseEntity, ITenantEntity
{
    public Guid OrganizationId { get; set; }
    public Guid? BranchId { get; set; }
    public Guid UserId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string Designation { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string? Qualification { get; set; }
    public string? Specialization { get; set; }
    public EmploymentType EmploymentType { get; set; } = EmploymentType.FullTime;
    public StaffStatus Status { get; set; } = StaffStatus.Active;
    public DateTime JoiningDate { get; set; } = DateTime.UtcNow;
    public decimal? BasicSalary { get; set; }
    public string? EmergencyContactName { get; set; }
    public string? EmergencyContactPhone { get; set; }
    public string? NationalId { get; set; }

    public Organization? Organization { get; set; }
    public Branch? Branch { get; set; }
    public User? User { get; set; }
}
