using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Entities.Tenant;

public class OrganizationSetting : BaseEntity, ITenantEntity
{
    public Guid OrganizationId { get; set; }
    public Guid? BranchId { get; set; }
    public string Currency { get; set; } = "PKR";
    public string DateFormat { get; set; } = "yyyy-MM-dd";
    public string TimeFormat { get; set; } = "HH:mm";
    public string TimeZone { get; set; } = "Pakistan Standard Time";
    public string Language { get; set; } = "en";
    public string GradingSystem { get; set; } = "Percentage"; // "Percentage", "GPA", "Letter"
    public decimal PassingMarksPercentage { get; set; } = 40.0m;
    public string WorkingDays { get; set; } = "Monday,Tuesday,Wednesday,Thursday,Friday";
    public string WorkingHours { get; set; } = "08:00 - 14:00";
    public string StudentIdFormat { get; set; } = "STU-{yyyy}-{0000}";
    public string EmployeeIdFormat { get; set; } = "EMP-{yyyy}-{0000}";
    public decimal AttendanceThresholdPercentage { get; set; } = 75.0m;

    public Organization? Organization { get; set; }
}
