using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Entities.Tenant;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Domain.Entities.Staff;

public class PaySlip : BaseEntity, ITenantEntity
{
    public Guid OrganizationId { get; set; }
    public Guid? BranchId { get; set; }
    public Guid PayrollRunId { get; set; }
    public Guid StaffProfileId { get; set; }
    public string SlipNumber { get; set; } = string.Empty;
    public int Month { get; set; }
    public int Year { get; set; }

    // Earnings
    public decimal BasicSalary { get; set; }
    public decimal HouseRentAllowance { get; set; } = 0m;
    public decimal MedicalAllowance { get; set; } = 0m;
    public decimal ConveyanceAllowance { get; set; } = 0m;
    public decimal SpecialAllowance { get; set; } = 0m;
    public decimal GrossSalary { get; set; }

    // Deductions
    public decimal TaxDeduction { get; set; } = 0m;
    public decimal EobiEmployeeContribution { get; set; } = 0m;
    public decimal EobiEmployerContribution { get; set; } = 0m;
    public int AbsenceDays { get; set; } = 0;
    public decimal AbsenceDeduction { get; set; } = 0m;
    public decimal AdvanceRecovery { get; set; } = 0m;
    public decimal OtherDeductions { get; set; } = 0m;
    public decimal TotalDeductions { get; set; }
    public decimal NetSalary { get; set; }

    // Payment details
    public SalaryPaymentMode PaymentMode { get; set; } = SalaryPaymentMode.BankTransfer;
    public string? BankName { get; set; }
    public string? BankAccountNumber { get; set; }
    public string? PaymentReference { get; set; }
    public PaySlipStatus Status { get; set; } = PaySlipStatus.Generated;
    public DateTime? PaidDate { get; set; }
    public string? Remarks { get; set; }

    // Navigation
    public Organization? Organization { get; set; }
    public Branch? Branch { get; set; }
    public PayrollRun? PayrollRun { get; set; }
    public StaffProfile? StaffProfile { get; set; }
}
