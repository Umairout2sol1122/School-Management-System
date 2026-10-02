using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Entities.Tenant;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Domain.Entities.Staff;

public class PayrollRun : BaseEntity, ITenantEntity
{
    public Guid OrganizationId { get; set; }
    public Guid? BranchId { get; set; }
    public int Month { get; set; }
    public int Year { get; set; }
    public string Title { get; set; } = string.Empty;
    public int TotalStaffCount { get; set; } = 0;
    public decimal TotalGrossSalary { get; set; } = 0m;
    public decimal TotalTaxDeduction { get; set; } = 0m;
    public decimal TotalEobiDeduction { get; set; } = 0m;
    public decimal TotalAbsenceDeduction { get; set; } = 0m;
    public decimal TotalOtherDeductions { get; set; } = 0m;
    public decimal TotalNetSalary { get; set; } = 0m;
    public PayrollStatus Status { get; set; } = PayrollStatus.Draft;
    public DateTime ProcessedDate { get; set; } = DateTime.UtcNow;
    public Guid? ProcessedByUserId { get; set; }
    public string? ProcessedByName { get; set; }
    public DateTime? ApprovedDate { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public string? ApprovedByName { get; set; }
    public DateTime? DisbursedDate { get; set; }
    public Guid? DisbursedByUserId { get; set; }
    public string? DisbursedByName { get; set; }
    public string? Remarks { get; set; }

    // Navigation
    public Organization? Organization { get; set; }
    public Branch? Branch { get; set; }
    public ICollection<PaySlip> PaySlips { get; set; } = new List<PaySlip>();
}
