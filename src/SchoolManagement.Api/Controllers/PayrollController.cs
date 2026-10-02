using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolManagement.Api.Authorization;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.Common.Models;
using SchoolManagement.Domain.Constants;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PayrollController : BaseApiController
{
    private readonly IPayrollService _payrollService;
    private readonly IAuditService _auditService;

    public PayrollController(
        IPayrollService payrollService,
        IAuditService auditService)
    {
        _payrollService = payrollService;
        _auditService = auditService;
    }

    [HttpGet("runs")]
    [RequirePermission(Permissions.TeachersView)]
    public async Task<IActionResult> GetPayrollRuns(
        [FromQuery] Guid? branchId,
        [FromQuery] int? year,
        CancellationToken cancellationToken = default)
    {
        var runs = await _payrollService.GetPayrollRunsAsync(branchId, year, cancellationToken);
        return ApiSuccess(runs);
    }

    [HttpGet("runs/{id:guid}")]
    [RequirePermission(Permissions.TeachersView)]
    public async Task<IActionResult> GetPayrollRun(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var run = await _payrollService.GetPayrollRunByIdAsync(id, cancellationToken);
        if (run == null)
        {
            return ApiError("Payroll run not found.", StatusCodes.Status404NotFound);
        }

        return ApiSuccess(run);
    }

    [HttpGet("runs/{id:guid}/slips")]
    [RequirePermission(Permissions.TeachersView)]
    public async Task<IActionResult> GetPaySlipsForRun(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var slips = await _payrollService.GetPaySlipsForRunAsync(id, cancellationToken);
        return ApiSuccess(slips);
    }

    [HttpPost("generate")]
    [RequirePermission(Permissions.TeachersEdit)]
    public async Task<IActionResult> GenerateMonthlyPayroll(
        [FromBody] GeneratePayrollRunRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var userId = CurrentUserService.UserId ?? Guid.Empty;
            var userName = CurrentUserService.Email ?? "Staff";

            var run = await _payrollService.GenerateMonthlyPayrollAsync(request, userId, userName, cancellationToken);

            await _auditService.LogAsync(
                AuditAction.Create,
                "Payroll",
                "PayrollRun",
                run.Id.ToString(),
                $"Generated monthly payroll for {run.Month:D2}/{run.Year} ({run.TotalStaffCount} staff, Net: PKR {run.TotalNetSalary:N2})",
                userId: userId,
                userEmail: userName,
                cancellationToken: cancellationToken);

            return StatusCode(StatusCodes.Status201Created, new
            {
                success = true,
                message = $"Payroll run for {run.Month:D2}/{run.Year} generated successfully.",
                data = run,
                timestampUtc = DateTime.UtcNow
            });
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return ApiError(ex.Message, StatusCodes.Status400BadRequest);
        }
        catch (InvalidOperationException ex)
        {
            return ApiError(ex.Message, StatusCodes.Status422UnprocessableEntity);
        }
    }

    [HttpPost("runs/{id:guid}/approve")]
    [RequirePermission(Permissions.TeachersEdit)]
    public async Task<IActionResult> ApprovePayrollRun(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserService.UserId ?? Guid.Empty;
        var userName = CurrentUserService.Email ?? "Staff";

        bool approved = await _payrollService.ApprovePayrollRunAsync(id, userId, userName, cancellationToken);
        if (!approved)
        {
            return ApiError("Payroll run not found or already approved/disbursed.", StatusCodes.Status400BadRequest);
        }

        await _auditService.LogAsync(
            AuditAction.Update,
            "Payroll",
            "PayrollRun",
            id.ToString(),
            $"Approved payroll run {id}",
            userId: userId,
            userEmail: userName,
            cancellationToken: cancellationToken);

        return ApiSuccess("Payroll run approved successfully.");
    }

    [HttpPost("runs/{id:guid}/disburse")]
    [RequirePermission(Permissions.TeachersEdit)]
    public async Task<IActionResult> DisbursePayrollRun(
        Guid id,
        [FromBody] DisbursePayrollRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserService.UserId ?? Guid.Empty;
        var userName = CurrentUserService.Email ?? "Staff";

        bool disbursed = await _payrollService.DisbursePayrollRunAsync(id, request, userId, userName, cancellationToken);
        if (!disbursed)
        {
            return ApiError("Payroll run must be approved before disbursement, or not found.", StatusCodes.Status400BadRequest);
        }

        await _auditService.LogAsync(
            AuditAction.Update,
            "Payroll",
            "PayrollRun",
            id.ToString(),
            $"Disbursed payroll run {id} via {request.PaymentMode}. Ref: {request.PaymentReference}",
            userId: userId,
            userEmail: userName,
            cancellationToken: cancellationToken);

        return ApiSuccess("Payroll disbursed and pay slips marked as paid.");
    }

    [HttpGet("slips/{id:guid}")]
    [RequirePermission(Permissions.TeachersView)]
    public async Task<IActionResult> GetPaySlip(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var slip = await _payrollService.GetPaySlipByIdAsync(id, cancellationToken);
        if (slip == null)
        {
            return ApiError("Pay slip not found.", StatusCodes.Status404NotFound);
        }

        return ApiSuccess(slip);
    }

    [HttpGet("salary-structure/{staffProfileId:guid}")]
    [RequirePermission(Permissions.TeachersView)]
    public async Task<IActionResult> GetSalaryStructure(
        Guid staffProfileId,
        CancellationToken cancellationToken = default)
    {
        var structure = await _payrollService.GetStaffSalaryStructureAsync(staffProfileId, cancellationToken);
        return ApiSuccess(structure);
    }

    [HttpPost("salary-structure")]
    [RequirePermission(Permissions.TeachersEdit)]
    public async Task<IActionResult> SetSalaryStructure(
        [FromBody] CreateSalaryStructureRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var structure = await _payrollService.SetStaffSalaryStructureAsync(request, cancellationToken);

            var userId = CurrentUserService.UserId ?? Guid.Empty;
            await _auditService.LogAsync(
                AuditAction.Create,
                "Payroll",
                "SalaryStructure",
                structure.Id.ToString(),
                $"Configured salary structure for {structure.StaffName} ({structure.EmployeeCode}): Basic PKR {structure.BasicSalary:N2}, Gross PKR {structure.GrossSalary:N2}",
                userId: userId,
                userEmail: CurrentUserService.Email,
                cancellationToken: cancellationToken);

            return ApiSuccess(structure, "Salary structure configured successfully.");
        }
        catch (KeyNotFoundException ex)
        {
            return ApiError(ex.Message, StatusCodes.Status404NotFound);
        }
    }

    [HttpGet("calculate-tax")]
    [RequirePermission(Permissions.TeachersView)]
    public IActionResult CalculateTax([FromQuery] decimal annualGross)
    {
        var result = _payrollService.CalculatePakistaniIncomeTax(annualGross);
        return ApiSuccess(result);
    }
}
