using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Entities.Tenant;

namespace SchoolManagement.Domain.Entities.Staff;

public class SalaryStructure : BaseEntity, ITenantEntity
{
    public Guid OrganizationId { get; set; }
    public Guid? BranchId { get; set; }
    public Guid StaffProfileId { get; set; }
    public decimal BasicSalary { get; set; }
    public decimal HouseRentAllowance { get; set; } = 0m;
    public decimal MedicalAllowance { get; set; } = 0m;
    public decimal ConveyanceAllowance { get; set; } = 0m;
    public decimal SpecialAllowance { get; set; } = 0m;
    public decimal GrossSalary => BasicSalary + HouseRentAllowance + MedicalAllowance + ConveyanceAllowance + SpecialAllowance;
    public bool TaxExempt { get; set; } = false;
    public bool EobiEnabled { get; set; } = true;
    public string? BankName { get; set; }
    public string? BankAccountTitle { get; set; }
    public string? BankAccountNumber { get; set; }
    public DateTime EffectiveDate { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;

    // Navigation
    public Organization? Organization { get; set; }
    public Branch? Branch { get; set; }
    public StaffProfile? StaffProfile { get; set; }
}
