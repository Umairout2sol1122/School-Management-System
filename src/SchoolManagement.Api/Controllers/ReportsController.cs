using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.Common.Models;
using SchoolManagement.Domain.Constants;
using SchoolManagement.Domain.Entities.Staff;
using SchoolManagement.Domain.Entities.Student;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ReportsController : BaseApiController
{
    private readonly IApplicationDbContext _context;
    private readonly IAuditService _auditService;

    public ReportsController(IApplicationDbContext context, IAuditService auditService)
    {
        _context = context;
        _auditService = auditService;
    }

    private Guid GetTargetOrgId(Guid? requestOrgId = null)
    {
        return CurrentUserService.IsSuperAdmin && requestOrgId.HasValue && requestOrgId.Value != Guid.Empty
            ? requestOrgId.Value
            : (CurrentUserService.OrganizationId ?? Guid.Empty);
    }

    [HttpGet("students")]
    public async Task<IActionResult> GetStudentReport(
        [FromQuery] string? grade,
        [FromQuery] string? section,
        [FromQuery] Guid? branchId,
        [FromQuery] StudentStatus? status,
        CancellationToken cancellationToken)
    {
        Guid targetOrgId = GetTargetOrgId();
        if (targetOrgId == Guid.Empty)
            return ApiError("Tenant context required.", StatusCodes.Status400BadRequest);

        var query = _context.Students
            .Include(s => s.Branch)
            .Include(s => s.StudentGuardians)
                .ThenInclude(sg => sg.Guardian)
            .Where(s => s.OrganizationId == targetOrgId && !s.IsDeleted);

        if (branchId.HasValue)
            query = query.Where(s => s.BranchId == branchId.Value);

        if (!string.IsNullOrWhiteSpace(grade))
            query = query.Where(s => s.CurrentGrade == grade.Trim());

        if (!string.IsNullOrWhiteSpace(section))
            query = query.Where(s => s.Section == section.Trim());

        if (status.HasValue)
            query = query.Where(s => s.Status == status.Value);

        var students = await query
            .OrderBy(s => s.CurrentGrade)
            .ThenBy(s => s.FirstName)
            .ToListAsync(cancellationToken);

        var list = students.Select(s => new StudentReportRowDto(
            s.Id,
            s.AdmissionNumber,
            s.RollNumber,
            s.FullName,
            s.CurrentGrade,
            s.Section,
            s.Branch != null ? s.Branch.Name : "Main Campus",
            s.Gender.ToString(),
            s.Status.ToString(),
            s.AdmissionDate,
            s.StudentGuardians.Where(sg => sg.IsPrimaryContact).Select(sg => sg.Guardian != null ? sg.Guardian.FullName : null).FirstOrDefault(),
            s.StudentGuardians.Where(sg => sg.IsPrimaryContact).Select(sg => sg.Guardian != null ? sg.Guardian.Phone : null).FirstOrDefault()
        )).ToList();

        return ApiSuccess(list);
    }

    [HttpGet("staff")]
    public async Task<IActionResult> GetStaffReport(
        [FromQuery] string? department,
        [FromQuery] string? designation,
        [FromQuery] Guid? branchId,
        [FromQuery] EmploymentType? employmentType,
        CancellationToken cancellationToken)
    {
        Guid targetOrgId = GetTargetOrgId();
        if (targetOrgId == Guid.Empty)
            return ApiError("Tenant context required.", StatusCodes.Status400BadRequest);

        var query = _context.StaffProfiles
            .Include(s => s.User)
            .Include(s => s.Branch)
            .Where(s => s.OrganizationId == targetOrgId && !s.IsDeleted);

        if (branchId.HasValue)
            query = query.Where(s => s.BranchId == branchId.Value);

        if (!string.IsNullOrWhiteSpace(department))
            query = query.Where(s => s.Department == department.Trim());

        if (!string.IsNullOrWhiteSpace(designation))
            query = query.Where(s => s.Designation == designation.Trim());

        if (employmentType.HasValue)
            query = query.Where(s => s.EmploymentType == employmentType.Value);

        var staffList = await query
            .OrderBy(s => s.Department)
            .ThenBy(s => s.User != null ? s.User.FirstName : string.Empty)
            .ToListAsync(cancellationToken);

        var list = staffList.Select(s => new StaffReportRowDto(
            s.Id,
            s.EmployeeCode,
            s.User != null ? s.User.FullName : "N/A",
            s.User != null ? s.User.Email : "N/A",
            s.User != null ? (s.User.PhoneNumber ?? "-") : "-",
            s.Designation,
            s.Department,
            s.Branch != null ? s.Branch.Name : "Main Campus",
            s.EmploymentType.ToString(),
            s.Status.ToString(),
            s.JoiningDate
        )).ToList();

        return ApiSuccess(list);
    }

    [HttpGet("branches")]
    public async Task<IActionResult> GetBranchReport(CancellationToken cancellationToken)
    {
        Guid targetOrgId = GetTargetOrgId();
        if (targetOrgId == Guid.Empty)
            return ApiError("Tenant context required.", StatusCodes.Status400BadRequest);

        var branches = await _context.Branches
            .Where(b => b.OrganizationId == targetOrgId && !b.IsDeleted)
            .OrderBy(b => b.IsMainBranch ? 0 : 1)
            .ThenBy(b => b.Name)
            .Select(b => new BranchReportRowDto(
                b.Id,
                b.Name,
                b.Code,
                b.Address,
                b.Phone,
                b.IsMainBranch,
                b.IsActive,
                _context.Students.Count(s => s.BranchId == b.Id && !s.IsDeleted),
                _context.StaffProfiles.Count(sp => sp.BranchId == b.Id && !sp.IsDeleted)
            ))
            .ToListAsync(cancellationToken);

        return ApiSuccess(branches);
    }

    [HttpGet("classes")]
    public async Task<IActionResult> GetClassUtilizationReport(CancellationToken cancellationToken)
    {
        Guid targetOrgId = GetTargetOrgId();
        if (targetOrgId == Guid.Empty)
            return ApiError("Tenant context required.", StatusCodes.Status400BadRequest);

        var classes = await _context.ClassGrades
            .Include(c => c.Department)
            .Include(c => c.Sections)
            .Where(c => c.OrganizationId == targetOrgId && !c.IsDeleted)
            .OrderBy(c => c.DisplayOrder)
            .Select(c => new ClassUtilizationReportRowDto(
                c.Id,
                c.Name,
                c.Code,
                c.Department != null ? c.Department.Name : "General",
                c.Capacity,
                _context.Students.Count(s => s.CurrentGrade == c.Name && s.OrganizationId == targetOrgId && !s.IsDeleted),
                c.Sections.Count(s => !s.IsDeleted),
                c.Capacity > 0
                    ? Math.Round(((double)_context.Students.Count(s => s.CurrentGrade == c.Name && s.OrganizationId == targetOrgId && !s.IsDeleted) / c.Capacity) * 100, 1)
                    : 0.0
            ))
            .ToListAsync(cancellationToken);

        return ApiSuccess(classes);
    }

    [HttpGet("attendance")]
    public async Task<IActionResult> GetAttendanceReport(
        [FromQuery] string? grade,
        [FromQuery] string? section,
        [FromQuery] Guid? branchId,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        CancellationToken cancellationToken = default)
    {
        Guid targetOrgId = GetTargetOrgId();
        if (targetOrgId == Guid.Empty)
            return ApiError("Tenant context required.", StatusCodes.Status400BadRequest);

        var query = _context.AttendanceRecords
            .Include(a => a.Student)
            .Include(a => a.Branch)
            .Where(a => a.OrganizationId == targetOrgId && !a.IsDeleted);

        if (!CurrentUserService.IsSuperAdmin && CurrentUserService.BranchId.HasValue && CurrentUserService.RoleType == UserRoleType.Principal)
        {
            query = query.Where(a => a.BranchId == CurrentUserService.BranchId.Value);
        }
        else if (branchId.HasValue)
        {
            query = query.Where(a => a.BranchId == branchId.Value);
        }

        if (fromDate.HasValue)
            query = query.Where(a => a.Date >= fromDate.Value.Date);

        if (toDate.HasValue)
            query = query.Where(a => a.Date <= toDate.Value.Date);

        var records = await query.ToListAsync(cancellationToken);

        var grouped = records
            .GroupBy(r => new { Grade = r.Student?.CurrentGrade ?? "Unassigned", Section = r.Student?.Section, BranchName = r.Branch?.Name })
            .Select(g =>
            {
                int total = g.Count();
                int present = g.Count(r => r.Status == AttendanceStatus.Present);
                int absent = g.Count(r => r.Status == AttendanceStatus.Absent);
                int late = g.Count(r => r.Status == AttendanceStatus.Late);
                int leave = g.Count(r => r.Status == AttendanceStatus.Leave);
                double pct = total > 0 ? Math.Round(((double)(present + late) / total) * 100, 1) : 0.0;

                return new AttendanceReportRowDto(
                    g.Key.Grade,
                    g.Key.Section,
                    g.Key.BranchName,
                    total,
                    present,
                    absent,
                    late,
                    leave,
                    pct
                );
            })
            .OrderBy(r => r.ClassGrade)
            .ThenBy(r => r.Section)
            .ToList();

        return ApiSuccess(grouped);
    }

    [HttpGet("fees")]
    public async Task<IActionResult> GetFeeReport(
        [FromQuery] string? grade,
        [FromQuery] Guid? branchId,
        CancellationToken cancellationToken = default)
    {
        Guid targetOrgId = GetTargetOrgId();
        if (targetOrgId == Guid.Empty)
            return ApiError("Tenant context required.", StatusCodes.Status400BadRequest);

        var query = _context.FeeInvoices
            .Include(f => f.Student)
            .Include(f => f.Branch)
            .Where(f => f.OrganizationId == targetOrgId && !f.IsDeleted);

        if (!CurrentUserService.IsSuperAdmin && CurrentUserService.BranchId.HasValue && CurrentUserService.RoleType == UserRoleType.Principal)
        {
            query = query.Where(f => f.BranchId == CurrentUserService.BranchId.Value);
        }
        else if (branchId.HasValue)
        {
            query = query.Where(f => f.BranchId == branchId.Value);
        }

        if (!string.IsNullOrWhiteSpace(grade))
        {
            query = query.Where(f => f.Student != null && f.Student.CurrentGrade == grade.Trim());
        }

        var invoices = await query.ToListAsync(cancellationToken);

        var grouped = invoices
            .GroupBy(f => new { Grade = f.Student?.CurrentGrade ?? "Unassigned", Section = f.Student?.Section, BranchName = f.Branch?.Name })
            .Select(g =>
            {
                int totalInvoices = g.Count();
                decimal totalBilled = g.Sum(x => x.TotalAmount);
                decimal totalPaid = g.Sum(x => x.PaidAmount);
                decimal balance = g.Sum(x => x.Balance);
                double pct = totalBilled > 0 ? Math.Round((double)(totalPaid / totalBilled) * 100, 1) : 0.0;
                int defaulters = g.Where(x => x.DueDate < DateTime.UtcNow.Date && x.Status != FeeStatus.Paid).Select(x => x.StudentId).Distinct().Count();

                return new FeeCollectionReportRowDto(
                    g.Key.Grade,
                    g.Key.Section,
                    g.Key.BranchName,
                    totalInvoices,
                    totalBilled,
                    totalPaid,
                    balance,
                    pct,
                    defaulters
                );
            })
            .OrderBy(r => r.ClassGrade)
            .ThenBy(r => r.Section)
            .ToList();

        return ApiSuccess(grouped);
    }

    [HttpGet("exams")]
    public async Task<IActionResult> GetExamReport(
        [FromQuery] Guid? examId,
        [FromQuery] string? grade,
        CancellationToken cancellationToken = default)
    {
        Guid targetOrgId = GetTargetOrgId();
        if (targetOrgId == Guid.Empty)
            return ApiError("Tenant context required.", StatusCodes.Status400BadRequest);

        var query = _context.ExamSchedules
            .Include(e => e.SubjectCourse)
            .Include(e => e.ClassGrade)
            .Include(e => e.Results)
            .Where(e => e.OrganizationId == targetOrgId && !e.IsDeleted);

        if (examId.HasValue)
            query = query.Where(e => e.Id == examId.Value);

        if (!string.IsNullOrWhiteSpace(grade))
            query = query.Where(e => e.ClassGrade != null && e.ClassGrade.Name == grade.Trim());

        var exams = await query.ToListAsync(cancellationToken);

        var report = exams.Select(e =>
        {
            var validResults = e.Results.Where(r => !r.IsDeleted).ToList();
            int total = validResults.Count;
            int passed = validResults.Count(r => r.Grade != "F" && r.Percentage >= 40m);
            int failed = total - passed;
            double passPct = total > 0 ? Math.Round(((double)passed / total) * 100, 1) : 0.0;
            decimal avg = total > 0 ? Math.Round(validResults.Average(r => r.MarksObtained), 1) : 0m;
            decimal highest = total > 0 ? validResults.Max(r => r.MarksObtained) : 0m;
            decimal lowest = total > 0 ? validResults.Min(r => r.MarksObtained) : 0m;

            return new ExamPerformanceReportRowDto(
                e.Id,
                e.Title,
                e.SubjectCourse?.Name ?? "General",
                e.ClassGrade?.Name ?? "General",
                total,
                passed,
                failed,
                passPct,
                avg,
                highest,
                lowest
            );
        }).ToList();

        return ApiSuccess(report);
    }

    [HttpGet("export/{type}")]
    public async Task<IActionResult> ExportCsv(string type, CancellationToken cancellationToken)
    {
        Guid targetOrgId = GetTargetOrgId();
        if (targetOrgId == Guid.Empty)
            return ApiError("Tenant context required.", StatusCodes.Status400BadRequest);

        var sb = new StringBuilder();

        switch (type.ToLower())
        {
            case "students":
                sb.AppendLine("AdmissionNumber,RollNumber,FirstName,LastName,Grade,Section,Gender,Status,AdmissionDate");
                var students = await _context.Students
                    .Where(s => s.OrganizationId == targetOrgId && !s.IsDeleted)
                    .OrderBy(s => s.CurrentGrade).ThenBy(s => s.AdmissionNumber)
                    .ToListAsync(cancellationToken);

                foreach (var s in students)
                {
                    sb.AppendLine($"\"{s.AdmissionNumber}\",\"{s.RollNumber}\",\"{s.FirstName}\",\"{s.LastName}\",\"{s.CurrentGrade}\",\"{s.Section}\",\"{s.Gender}\",\"{s.Status}\",\"{s.AdmissionDate:yyyy-MM-dd}\"");
                }
                break;

            case "staff":
                sb.AppendLine("EmployeeCode,FirstName,LastName,Email,Designation,Department,EmploymentType,Status,JoiningDate");
                var staff = await _context.StaffProfiles
                    .Include(sp => sp.User)
                    .Where(sp => sp.OrganizationId == targetOrgId && !sp.IsDeleted)
                    .ToListAsync(cancellationToken);

                foreach (var sp in staff)
                {
                    sb.AppendLine($"\"{sp.EmployeeCode}\",\"{sp.User?.FirstName}\",\"{sp.User?.LastName}\",\"{sp.User?.Email}\",\"{sp.Designation}\",\"{sp.Department}\",\"{sp.EmploymentType}\",\"{sp.Status}\",\"{sp.JoiningDate:yyyy-MM-dd}\"");
                }
                break;

            case "classes":
                sb.AppendLine("Code,Name,DisplayOrder,Capacity,IsActive");
                var classes = await _context.ClassGrades
                    .Where(c => c.OrganizationId == targetOrgId && !c.IsDeleted)
                    .OrderBy(c => c.DisplayOrder)
                    .ToListAsync(cancellationToken);

                foreach (var c in classes)
                {
                    sb.AppendLine($"\"{c.Code}\",\"{c.Name}\",{c.DisplayOrder},{c.Capacity},\"{c.IsActive}\"");
                }
                break;

            case "subjects":
                sb.AppendLine("Code,Name,CreditHours,Type,IsActive");
                var subjects = await _context.SubjectCourses
                    .Where(s => s.OrganizationId == targetOrgId && !s.IsDeleted)
                    .OrderBy(s => s.Code)
                    .ToListAsync(cancellationToken);

                foreach (var sub in subjects)
                {
                    sb.AppendLine($"\"{sub.Code}\",\"{sub.Name}\",{sub.CreditHours},\"{sub.Type}\",\"{sub.IsActive}\"");
                }
                break;

            case "fees":
                sb.AppendLine("InvoiceNumber,StudentAdmission,Title,IssueDate,DueDate,TotalAmount,PaidAmount,Balance,Status");
                var invoices = await _context.FeeInvoices
                    .Include(f => f.Student)
                    .Where(f => f.OrganizationId == targetOrgId && !f.IsDeleted)
                    .OrderByDescending(f => f.IssueDate)
                    .ToListAsync(cancellationToken);

                foreach (var inv in invoices)
                {
                    sb.AppendLine($"\"{inv.InvoiceNumber}\",\"{inv.Student?.AdmissionNumber}\",\"{inv.Title}\",\"{inv.IssueDate:yyyy-MM-dd}\",\"{inv.DueDate:yyyy-MM-dd}\",{inv.TotalAmount},{inv.PaidAmount},{inv.Balance},\"{inv.Status}\"");
                }
                break;

            default:
                return ApiError("Invalid export type. Supported types: students, staff, classes, subjects, fees.");
        }

        byte[] bytes = Encoding.UTF8.GetBytes(sb.ToString());
        return File(bytes, "text/csv", $"{type}_export_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv");
    }

    [HttpPost("import/students")]
    public async Task<IActionResult> ImportStudents([FromBody] ImportStudentsRequest request, CancellationToken cancellationToken)
    {
        string? csvContent = request?.CsvContent;
        if (string.IsNullOrWhiteSpace(csvContent))
            return ApiError("CSV content is required.");

        Guid targetOrgId = GetTargetOrgId();
        if (targetOrgId == Guid.Empty)
            return ApiError("Tenant context required.", StatusCodes.Status400BadRequest);

        var lines = csvContent.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length <= 1)
            return ApiError("CSV content must have a header line and at least one data row.");

        int success = 0;
        int failure = 0;
        var errors = new List<string>();

        // Find main branch
        var mainBranch = await _context.Branches
            .FirstOrDefaultAsync(b => b.OrganizationId == targetOrgId && b.IsMainBranch && !b.IsDeleted, cancellationToken)
            ?? await _context.Branches.FirstOrDefaultAsync(b => b.OrganizationId == targetOrgId && !b.IsDeleted, cancellationToken);

        for (int i = 1; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrWhiteSpace(line)) continue;

            var cols = line.Split(',').Select(c => c.Trim(' ', '"')).ToArray();
            if (cols.Length < 4)
            {
                failure++;
                errors.Add($"Row {i + 1}: Insufficient columns. Minimum required: AdmissionNumber, FirstName, LastName, Grade");
                continue;
            }

            string adm = cols[0];
            string fn = cols[1];
            string ln = cols.Length > 2 ? cols[2] : "Student";
            string grade = cols.Length > 3 ? cols[3] : "General";
            string? sec = cols.Length > 4 ? cols[4] : null;

            if (string.IsNullOrWhiteSpace(fn) || string.IsNullOrWhiteSpace(adm))
            {
                failure++;
                errors.Add($"Row {i + 1}: Admission number and student first name cannot be blank.");
                continue;
            }

            bool exists = await _context.Students.AnyAsync(s => s.OrganizationId == targetOrgId && s.AdmissionNumber == adm, cancellationToken);
            if (exists)
            {
                failure++;
                errors.Add($"Row {i + 1}: Admission number '{adm}' already exists.");
                continue;
            }

            var student = new Student
            {
                Id = Guid.NewGuid(),
                OrganizationId = targetOrgId,
                BranchId = mainBranch?.Id,
                AdmissionNumber = adm,
                FirstName = fn,
                LastName = ln,
                CurrentGrade = grade,
                Section = sec,
                DateOfBirth = DateTime.UtcNow.AddYears(-10),
                AdmissionDate = DateTime.UtcNow,
                Status = StudentStatus.Active
            };

            _context.Students.Add(student);
            success++;
        }

        if (success > 0)
        {
            await _context.SaveChangesAsync(cancellationToken);
            await _auditService.LogAsync(
                AuditAction.Create, "Students", "Import", targetOrgId.ToString(),
                $"Bulk imported {success} students ({failure} failed)",
                organizationId: targetOrgId,
                userId: CurrentUserService.UserId,
                userEmail: CurrentUserService.Email,
                cancellationToken: cancellationToken);
        }

        var result = new BulkImportResultDto(lines.Length - 1, success, failure, errors);
        return ApiSuccess(result, $"Import completed: {success} added, {failure} failed.");
    }
}
