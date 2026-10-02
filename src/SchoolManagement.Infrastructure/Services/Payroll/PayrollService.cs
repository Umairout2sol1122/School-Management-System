using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.Common.Models;
using SchoolManagement.Domain.Entities.Staff;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.Infrastructure.Services.Payroll;

public class PayrollService : IPayrollService
{
    private readonly ApplicationDbContext _context;

    public PayrollService(ApplicationDbContext context)
    {
        _context = context;
    }

    public TaxCalculationResult CalculatePakistaniIncomeTax(decimal annualGross)
    {
        return PayrollTaxCalculator.CalculatePakistaniIncomeTax(annualGross);
    }

    public async Task<SalaryStructureDto> SetStaffSalaryStructureAsync(
        CreateSalaryStructureRequest request,
        CancellationToken cancellationToken = default)
    {
        var staff = await _context.StaffProfiles
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.Id == request.StaffProfileId && !s.IsDeleted, cancellationToken);

        if (staff == null)
        {
            throw new KeyNotFoundException($"Staff profile with ID '{request.StaffProfileId}' not found.");
        }

        // Deactivate previous active salary structures
        var existingStructures = await _context.SalaryStructures
            .Where(s => s.StaffProfileId == request.StaffProfileId && s.IsActive && !s.IsDeleted)
            .ToListAsync(cancellationToken);

        foreach (var existing in existingStructures)
        {
            existing.IsActive = false;
            existing.MarkUpdated("PayrollService");
        }

        // Apply realistic Pakistani school allowance defaults if null
        decimal basic = request.BasicSalary;
        decimal houseRent = request.HouseRentAllowance ?? Math.Round(basic * 0.45m, 2); // Standard 45%
        decimal medical = request.MedicalAllowance ?? Math.Round(basic * 0.10m, 2);     // Standard 10%
        decimal conveyance = request.ConveyanceAllowance ?? 5000m;                     // Standard PKR 5,000
        decimal special = request.SpecialAllowance ?? 0m;

        var structure = new SalaryStructure
        {
            OrganizationId = staff.OrganizationId,
            BranchId = staff.BranchId,
            StaffProfileId = staff.Id,
            BasicSalary = basic,
            HouseRentAllowance = houseRent,
            MedicalAllowance = medical,
            ConveyanceAllowance = conveyance,
            SpecialAllowance = special,
            TaxExempt = request.TaxExempt,
            EobiEnabled = request.EobiEnabled,
            BankName = request.BankName,
            BankAccountTitle = request.BankAccountTitle ?? staff.User?.FullName,
            BankAccountNumber = request.BankAccountNumber,
            EffectiveDate = DateTime.UtcNow,
            IsActive = true
        };

        _context.SalaryStructures.Add(structure);

        // Update staff profile basic salary for quick reference
        staff.BasicSalary = basic;
        staff.MarkUpdated("PayrollService");

        await _context.SaveChangesAsync(cancellationToken);

        return MapSalaryStructure(structure, staff);
    }

    public async Task<SalaryStructureDto?> GetStaffSalaryStructureAsync(
        Guid staffProfileId,
        CancellationToken cancellationToken = default)
    {
        var structure = await _context.SalaryStructures
            .Include(s => s.StaffProfile)
                .ThenInclude(sp => sp!.User)
            .FirstOrDefaultAsync(s => s.StaffProfileId == staffProfileId && s.IsActive && !s.IsDeleted, cancellationToken);

        if (structure == null) return null;
        return MapSalaryStructure(structure, structure.StaffProfile);
    }

    public async Task<PayrollRunDto> GenerateMonthlyPayrollAsync(
        GeneratePayrollRunRequest request,
        Guid currentUserId,
        string currentUserName,
        CancellationToken cancellationToken = default)
    {
        if (request.Month < 1 || request.Month > 12)
        {
            throw new ArgumentOutOfRangeException(nameof(request.Month), "Month must be between 1 and 12.");
        }

        if (request.Year < 2020 || request.Year > 2050)
        {
            throw new ArgumentOutOfRangeException(nameof(request.Year), "Invalid year.");
        }

        // Check for existing run
        var existingRun = await _context.PayrollRuns
            .Include(r => r.PaySlips)
            .FirstOrDefaultAsync(r => r.Month == request.Month &&
                                      r.Year == request.Year &&
                                      (request.BranchId == null || r.BranchId == request.BranchId) &&
                                      !r.IsDeleted, cancellationToken);

        if (existingRun != null && existingRun.Status != PayrollStatus.Draft)
        {
            throw new InvalidOperationException($"Payroll for {request.Month:D2}/{request.Year} is already {existingRun.Status} and cannot be regenerated.");
        }

        // Query active staff
        var staffQuery = _context.StaffProfiles
            .Include(s => s.User)
            .Include(s => s.Branch)
            .Where(s => s.Status == StaffStatus.Active && !s.IsDeleted);

        if (request.BranchId.HasValue)
        {
            staffQuery = staffQuery.Where(s => s.BranchId == request.BranchId.Value);
        }

        var staffList = await staffQuery.ToListAsync(cancellationToken);
        if (!staffList.Any())
        {
            throw new InvalidOperationException("No active staff found to generate payroll.");
        }

        // Get active salary structures
        var staffIds = staffList.Select(s => s.Id).ToList();
        var structures = await _context.SalaryStructures
            .Where(s => staffIds.Contains(s.StaffProfileId) && s.IsActive && !s.IsDeleted)
            .ToDictionaryAsync(s => s.StaffProfileId, cancellationToken);

        // Prepare or reuse run
        var payrollRun = existingRun;
        if (payrollRun == null)
        {
            var firstStaff = staffList.First();
            string monthName = new DateTime(request.Year, request.Month, 1).ToString("MMMM yyyy");
            payrollRun = new PayrollRun
            {
                OrganizationId = firstStaff.OrganizationId,
                BranchId = request.BranchId,
                Month = request.Month,
                Year = request.Year,
                Title = request.Title ?? $"{monthName} Staff Payroll",
                Status = PayrollStatus.Draft,
                ProcessedDate = DateTime.UtcNow,
                ProcessedByUserId = currentUserId,
                ProcessedByName = currentUserName
            };
            _context.PayrollRuns.Add(payrollRun);
        }
        else
        {
            // Clear draft slips for regeneration
            _context.PaySlips.RemoveRange(payrollRun.PaySlips);
            payrollRun.ProcessedDate = DateTime.UtcNow;
            payrollRun.ProcessedByUserId = currentUserId;
            payrollRun.ProcessedByName = currentUserName;
        }

        int slipSeq = 1;
        decimal totalGross = 0m;
        decimal totalTax = 0m;
        decimal totalEobi = 0m;
        decimal totalAbsence = 0m;
        decimal totalOther = 0m;
        decimal totalNet = 0m;

        var slipsToInsert = new List<PaySlip>();

        foreach (var staff in staffList)
        {
            decimal basic, houseRent, medical, conveyance, special;
            bool taxExempt = false;
            bool eobiEnabled = true;
            string? bankName = null;
            string? bankAccount = null;

            if (structures.TryGetValue(staff.Id, out var structure))
            {
                basic = structure.BasicSalary;
                houseRent = structure.HouseRentAllowance;
                medical = structure.MedicalAllowance;
                conveyance = structure.ConveyanceAllowance;
                special = structure.SpecialAllowance;
                taxExempt = structure.TaxExempt;
                eobiEnabled = structure.EobiEnabled;
                bankName = structure.BankName;
                bankAccount = structure.BankAccountNumber;
            }
            else
            {
                basic = staff.BasicSalary ?? 40000m;
                houseRent = Math.Round(basic * 0.45m, 2);
                medical = Math.Round(basic * 0.10m, 2);
                conveyance = 5000m;
                special = 0m;
            }

            decimal gross = basic + houseRent + medical + conveyance + special;

            // Absence calculation
            int absenceDays = request.StaffAbsenceDays != null && request.StaffAbsenceDays.TryGetValue(staff.Id, out int abs) ? abs : 0;
            decimal absenceDeduction = PayrollTaxCalculator.CalculateAbsenceDeduction(gross, absenceDays);

            // Tax calculation
            decimal taxDeduction = 0m;
            if (!taxExempt)
            {
                var taxResult = PayrollTaxCalculator.CalculatePakistaniIncomeTax(gross * 12m);
                taxDeduction = taxResult.MonthlyTax;
            }

            // EOBI calculation
            var (empEobi, emprEobi) = PayrollTaxCalculator.CalculateEobi(eobiEnabled);

            // Advance recovery
            decimal advance = request.StaffAdvanceRecoveries != null && request.StaffAdvanceRecoveries.TryGetValue(staff.Id, out decimal adv) ? adv : 0m;

            decimal totalDeductions = taxDeduction + empEobi + absenceDeduction + advance;
            decimal net = Math.Max(0m, gross - totalDeductions);

            string branchCode = staff.Branch?.Code?.Trim().ToUpperInvariant() ?? "MAIN";
            string slipNumber = $"PS-{branchCode}-{request.Year}{request.Month:D2}-{slipSeq++:D4}";

            var paySlip = new PaySlip
            {
                OrganizationId = staff.OrganizationId,
                BranchId = staff.BranchId,
                PayrollRun = payrollRun,
                StaffProfileId = staff.Id,
                SlipNumber = slipNumber,
                Month = request.Month,
                Year = request.Year,
                BasicSalary = basic,
                HouseRentAllowance = houseRent,
                MedicalAllowance = medical,
                ConveyanceAllowance = conveyance,
                SpecialAllowance = special,
                GrossSalary = gross,
                TaxDeduction = taxDeduction,
                EobiEmployeeContribution = empEobi,
                EobiEmployerContribution = emprEobi,
                AbsenceDays = absenceDays,
                AbsenceDeduction = absenceDeduction,
                AdvanceRecovery = advance,
                TotalDeductions = totalDeductions,
                NetSalary = net,
                PaymentMode = SalaryPaymentMode.BankTransfer,
                BankName = bankName,
                BankAccountNumber = bankAccount,
                Status = PaySlipStatus.Generated
            };

            slipsToInsert.Add(paySlip);

            totalGross += gross;
            totalTax += taxDeduction;
            totalEobi += empEobi;
            totalAbsence += absenceDeduction;
            totalOther += advance;
            totalNet += net;
        }

        payrollRun.TotalStaffCount = staffList.Count;
        payrollRun.TotalGrossSalary = totalGross;
        payrollRun.TotalTaxDeduction = totalTax;
        payrollRun.TotalEobiDeduction = totalEobi;
        payrollRun.TotalAbsenceDeduction = totalAbsence;
        payrollRun.TotalOtherDeductions = totalOther;
        payrollRun.TotalNetSalary = totalNet;

        _context.PaySlips.AddRange(slipsToInsert);
        await _context.SaveChangesAsync(cancellationToken);

        return MapPayrollRun(payrollRun);
    }

    public async Task<bool> ApprovePayrollRunAsync(
        Guid runId,
        Guid currentUserId,
        string currentUserName,
        CancellationToken cancellationToken = default)
    {
        var run = await _context.PayrollRuns
            .FirstOrDefaultAsync(r => r.Id == runId && !r.IsDeleted, cancellationToken);

        if (run == null || run.Status != PayrollStatus.Draft) return false;

        run.Status = PayrollStatus.Approved;
        run.ApprovedDate = DateTime.UtcNow;
        run.ApprovedByUserId = currentUserId;
        run.ApprovedByName = currentUserName;
        run.MarkUpdated(currentUserName);

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DisbursePayrollRunAsync(
        Guid runId,
        DisbursePayrollRequest request,
        Guid currentUserId,
        string currentUserName,
        CancellationToken cancellationToken = default)
    {
        var run = await _context.PayrollRuns
            .Include(r => r.PaySlips)
            .FirstOrDefaultAsync(r => r.Id == runId && !r.IsDeleted, cancellationToken);

        if (run == null || run.Status != PayrollStatus.Approved) return false;

        run.Status = PayrollStatus.Disbursed;
        run.DisbursedDate = DateTime.UtcNow;
        run.DisbursedByUserId = currentUserId;
        run.DisbursedByName = currentUserName;
        run.Remarks = request.Remarks;
        run.MarkUpdated(currentUserName);

        foreach (var slip in run.PaySlips)
        {
            slip.Status = PaySlipStatus.Paid;
            slip.PaidDate = DateTime.UtcNow;
            slip.PaymentMode = request.PaymentMode;
            slip.PaymentReference = request.PaymentReference;
            slip.MarkUpdated(currentUserName);
        }

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<PayrollRunDto?> GetPayrollRunByIdAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        var run = await _context.PayrollRuns
            .Include(r => r.Branch)
            .FirstOrDefaultAsync(r => r.Id == runId && !r.IsDeleted, cancellationToken);

        return run == null ? null : MapPayrollRun(run);
    }

    public async Task<List<PayrollRunDto>> GetPayrollRunsAsync(
        Guid? branchId = null,
        int? year = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.PayrollRuns
            .Include(r => r.Branch)
            .Where(r => !r.IsDeleted);

        if (branchId.HasValue)
        {
            query = query.Where(r => r.BranchId == branchId.Value);
        }

        if (year.HasValue)
        {
            query = query.Where(r => r.Year == year.Value);
        }

        var runs = await query
            .OrderByDescending(r => r.Year)
            .ThenByDescending(r => r.Month)
            .ToListAsync(cancellationToken);

        return runs.Select(MapPayrollRun).ToList();
    }

    public async Task<List<PaySlipDto>> GetPaySlipsForRunAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        var slips = await _context.PaySlips
            .Include(p => p.StaffProfile)
                .ThenInclude(sp => sp!.User)
            .Where(p => p.PayrollRunId == runId && !p.IsDeleted)
            .OrderBy(p => p.SlipNumber)
            .ToListAsync(cancellationToken);

        return slips.Select(MapPaySlip).ToList();
    }

    public async Task<PaySlipDetailDto?> GetPaySlipByIdAsync(Guid paySlipId, CancellationToken cancellationToken = default)
    {
        var slip = await _context.PaySlips
            .Include(p => p.PayrollRun)
            .Include(p => p.Organization)
            .Include(p => p.Branch)
            .Include(p => p.StaffProfile)
                .ThenInclude(sp => sp!.User)
            .FirstOrDefaultAsync(p => p.Id == paySlipId && !p.IsDeleted, cancellationToken);

        if (slip == null) return null;

        var staff = slip.StaffProfile;
        var user = staff?.User;

        return new PaySlipDetailDto(
            slip.Id,
            slip.OrganizationId,
            slip.Organization?.Name ?? "School Management",
            slip.BranchId,
            slip.Branch?.Name,
            slip.PayrollRunId,
            slip.PayrollRun?.Title ?? "Monthly Payroll",
            slip.StaffProfileId,
            staff?.EmployeeCode ?? "EMP",
            user?.FullName ?? "Staff Member",
            user?.Email,
            user?.PhoneNumber,
            staff?.NationalId,
            staff?.Designation ?? "Faculty",
            staff?.Department ?? "General",
            slip.SlipNumber,
            slip.Month,
            slip.Year,
            slip.BasicSalary,
            slip.HouseRentAllowance,
            slip.MedicalAllowance,
            slip.ConveyanceAllowance,
            slip.SpecialAllowance,
            slip.GrossSalary,
            slip.TaxDeduction,
            slip.EobiEmployeeContribution,
            slip.EobiEmployerContribution,
            slip.AbsenceDays,
            slip.AbsenceDeduction,
            slip.AdvanceRecovery,
            slip.OtherDeductions,
            slip.TotalDeductions,
            slip.NetSalary,
            slip.PaymentMode,
            slip.BankName,
            slip.BankAccountNumber,
            slip.PaymentReference,
            slip.Status,
            slip.Status.ToString(),
            slip.PaidDate,
            slip.Remarks);
    }

    private static SalaryStructureDto MapSalaryStructure(SalaryStructure s, StaffProfile? staff)
    {
        return new SalaryStructureDto(
            s.Id,
            s.OrganizationId,
            s.BranchId,
            s.StaffProfileId,
            staff?.EmployeeCode ?? "EMP",
            staff?.User?.FullName ?? "Staff Member",
            staff?.Designation ?? "Faculty",
            staff?.Department ?? "General",
            s.BasicSalary,
            s.HouseRentAllowance,
            s.MedicalAllowance,
            s.ConveyanceAllowance,
            s.SpecialAllowance,
            s.GrossSalary,
            s.TaxExempt,
            s.EobiEnabled,
            s.BankName,
            s.BankAccountTitle,
            s.BankAccountNumber,
            s.EffectiveDate,
            s.IsActive);
    }

    private static PayrollRunDto MapPayrollRun(PayrollRun r)
    {
        return new PayrollRunDto(
            r.Id,
            r.OrganizationId,
            r.BranchId,
            r.Branch?.Name,
            r.Month,
            r.Year,
            r.Title,
            r.TotalStaffCount,
            r.TotalGrossSalary,
            r.TotalTaxDeduction,
            r.TotalEobiDeduction,
            r.TotalAbsenceDeduction,
            r.TotalOtherDeductions,
            r.TotalNetSalary,
            r.Status,
            r.Status.ToString(),
            r.ProcessedDate,
            r.ProcessedByName,
            r.ApprovedDate,
            r.ApprovedByName,
            r.DisbursedDate,
            r.DisbursedByName,
            r.Remarks);
    }

    private static PaySlipDto MapPaySlip(PaySlip p)
    {
        var staff = p.StaffProfile;
        return new PaySlipDto(
            p.Id,
            p.PayrollRunId,
            p.StaffProfileId,
            staff?.EmployeeCode ?? "EMP",
            staff?.User?.FullName ?? "Staff Member",
            staff?.Designation ?? "Faculty",
            staff?.Department ?? "General",
            p.SlipNumber,
            p.Month,
            p.Year,
            p.BasicSalary,
            p.GrossSalary,
            p.TaxDeduction,
            p.EobiEmployeeContribution,
            p.EobiEmployerContribution,
            p.AbsenceDays,
            p.AbsenceDeduction,
            p.AdvanceRecovery,
            p.TotalDeductions,
            p.NetSalary,
            p.PaymentMode,
            p.PaymentMode.ToString(),
            p.Status,
            p.Status.ToString(),
            p.PaidDate);
    }
}
