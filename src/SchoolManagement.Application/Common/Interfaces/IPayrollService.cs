using SchoolManagement.Application.Common.Models;

namespace SchoolManagement.Application.Common.Interfaces;

public interface IPayrollService
{
    Task<PayrollRunDto> GenerateMonthlyPayrollAsync(GeneratePayrollRunRequest request, Guid currentUserId, string currentUserName, CancellationToken cancellationToken = default);
    Task<PayrollRunDto?> GetPayrollRunByIdAsync(Guid runId, CancellationToken cancellationToken = default);
    Task<List<PayrollRunDto>> GetPayrollRunsAsync(Guid? branchId = null, int? year = null, CancellationToken cancellationToken = default);
    Task<List<PaySlipDto>> GetPaySlipsForRunAsync(Guid runId, CancellationToken cancellationToken = default);
    Task<PaySlipDetailDto?> GetPaySlipByIdAsync(Guid paySlipId, CancellationToken cancellationToken = default);
    Task<bool> ApprovePayrollRunAsync(Guid runId, Guid currentUserId, string currentUserName, CancellationToken cancellationToken = default);
    Task<bool> DisbursePayrollRunAsync(Guid runId, DisbursePayrollRequest request, Guid currentUserId, string currentUserName, CancellationToken cancellationToken = default);
    Task<SalaryStructureDto> SetStaffSalaryStructureAsync(CreateSalaryStructureRequest request, CancellationToken cancellationToken = default);
    Task<SalaryStructureDto?> GetStaffSalaryStructureAsync(Guid staffProfileId, CancellationToken cancellationToken = default);
    TaxCalculationResult CalculatePakistaniIncomeTax(decimal annualGross);
}
