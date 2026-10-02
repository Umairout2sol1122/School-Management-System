using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Application.Common.Models;

public record SalaryStructureDto(
    Guid Id,
    Guid OrganizationId,
    Guid? BranchId,
    Guid StaffProfileId,
    string EmployeeCode,
    string StaffName,
    string Designation,
    string Department,
    decimal BasicSalary,
    decimal HouseRentAllowance,
    decimal MedicalAllowance,
    decimal ConveyanceAllowance,
    decimal SpecialAllowance,
    decimal GrossSalary,
    bool TaxExempt,
    bool EobiEnabled,
    string? BankName,
    string? BankAccountTitle,
    string? BankAccountNumber,
    DateTime EffectiveDate,
    bool IsActive
);

public record CreateSalaryStructureRequest(
    Guid StaffProfileId,
    decimal BasicSalary,
    decimal? HouseRentAllowance = null,
    decimal? MedicalAllowance = null,
    decimal? ConveyanceAllowance = null,
    decimal? SpecialAllowance = null,
    bool TaxExempt = false,
    bool EobiEnabled = true,
    string? BankName = null,
    string? BankAccountTitle = null,
    string? BankAccountNumber = null
);

public record PayrollRunDto(
    Guid Id,
    Guid OrganizationId,
    Guid? BranchId,
    string? BranchName,
    int Month,
    int Year,
    string Title,
    int TotalStaffCount,
    decimal TotalGrossSalary,
    decimal TotalTaxDeduction,
    decimal TotalEobiDeduction,
    decimal TotalAbsenceDeduction,
    decimal TotalOtherDeductions,
    decimal TotalNetSalary,
    PayrollStatus Status,
    string StatusName,
    DateTime ProcessedDate,
    string? ProcessedByName,
    DateTime? ApprovedDate,
    string? ApprovedByName,
    DateTime? DisbursedDate,
    string? DisbursedByName,
    string? Remarks
);

public record GeneratePayrollRunRequest(
    int Month,
    int Year,
    Guid? BranchId = null,
    string? Title = null,
    Dictionary<Guid, int>? StaffAbsenceDays = null, // Custom absence days per staff profile
    Dictionary<Guid, decimal>? StaffAdvanceRecoveries = null
);

public record PaySlipDto(
    Guid Id,
    Guid PayrollRunId,
    Guid StaffProfileId,
    string EmployeeCode,
    string StaffName,
    string Designation,
    string Department,
    string SlipNumber,
    int Month,
    int Year,
    decimal BasicSalary,
    decimal GrossSalary,
    decimal TaxDeduction,
    decimal EobiEmployeeContribution,
    decimal EobiEmployerContribution,
    int AbsenceDays,
    decimal AbsenceDeduction,
    decimal AdvanceRecovery,
    decimal TotalDeductions,
    decimal NetSalary,
    SalaryPaymentMode PaymentMode,
    string PaymentModeName,
    PaySlipStatus Status,
    string StatusName,
    DateTime? PaidDate
);

public record PaySlipDetailDto(
    Guid Id,
    Guid OrganizationId,
    string OrganizationName,
    Guid? BranchId,
    string? BranchName,
    Guid PayrollRunId,
    string PayrollRunTitle,
    Guid StaffProfileId,
    string EmployeeCode,
    string StaffName,
    string? Email,
    string? Phone,
    string? NationalId,
    string Designation,
    string Department,
    string SlipNumber,
    int Month,
    int Year,
    decimal BasicSalary,
    decimal HouseRentAllowance,
    decimal MedicalAllowance,
    decimal ConveyanceAllowance,
    decimal SpecialAllowance,
    decimal GrossSalary,
    decimal TaxDeduction,
    decimal EobiEmployeeContribution,
    decimal EobiEmployerContribution,
    int AbsenceDays,
    decimal AbsenceDeduction,
    decimal AdvanceRecovery,
    decimal OtherDeductions,
    decimal TotalDeductions,
    decimal NetSalary,
    SalaryPaymentMode PaymentMode,
    string? BankName,
    string? BankAccountNumber,
    string? PaymentReference,
    PaySlipStatus Status,
    string StatusName,
    DateTime? PaidDate,
    string? Remarks
);

public record DisbursePayrollRequest(
    SalaryPaymentMode PaymentMode = SalaryPaymentMode.BankTransfer,
    string? PaymentReference = null,
    string? Remarks = null
);

public record TaxCalculationResult(
    decimal AnnualTaxableSalary,
    decimal AnnualTax,
    decimal MonthlyTax,
    string SlabDescription
);
