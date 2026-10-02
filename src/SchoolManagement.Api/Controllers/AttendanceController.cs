using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Api.Authorization;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.Common.Models;
using SchoolManagement.Domain.Constants;
using SchoolManagement.Domain.Entities.Academics;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AttendanceController : BaseApiController
{
    private readonly IApplicationDbContext _context;
    private readonly IAuditService _auditService;
    private readonly ISmsNotificationService? _smsNotificationService;

    public AttendanceController(
        IApplicationDbContext context,
        IAuditService auditService,
        ISmsNotificationService? smsNotificationService = null)
    {
        _context = context;
        _auditService = auditService;
        _smsNotificationService = smsNotificationService;
    }

    private Guid GetTargetOrgId()
    {
        return CurrentUserService.IsSuperAdmin ? (TenantContext.OrganizationId ?? Guid.Empty) : (CurrentUserService.OrganizationId ?? Guid.Empty);
    }

    // =========================================================================
    // 1. GET ATTENDANCE RECORDS (ADMIN / BRANCH OVERVIEW)
    // =========================================================================

    [HttpGet]
    [RequirePermission(Permissions.AttendanceView)]
    public async Task<IActionResult> GetAttendance(
        [FromQuery] DateTime? date,
        [FromQuery] Guid? branchId,
        [FromQuery] Guid? classGradeId,
        [FromQuery] Guid? classSectionId,
        [FromQuery] AttendanceStatus? status,
        CancellationToken cancellationToken = default)
    {
        Guid targetOrgId = GetTargetOrgId();
        if (targetOrgId == Guid.Empty)
            return ApiError("Tenant context required.", StatusCodes.Status400BadRequest);

        // Branch boundary enforcement
        if (CurrentUserService.RoleType == UserRoleType.Principal && CurrentUserService.BranchId.HasValue)
        {
            if (branchId.HasValue && branchId.Value != CurrentUserService.BranchId.Value)
                return ApiError("Unauthorized access to another branch's attendance.", StatusCodes.Status403Forbidden);

            branchId = CurrentUserService.BranchId.Value;
        }

        var targetDate = (date ?? DateTime.UtcNow).Date;

        var query = _context.AttendanceRecords
            .Include(a => a.Student)
            .Include(a => a.ClassGrade)
            .Include(a => a.ClassSection)
            .Include(a => a.MarkedByStaff).ThenInclude(s => s!.User)
            .Where(a => a.OrganizationId == targetOrgId &&
                        a.Date >= targetDate && a.Date < targetDate.AddDays(1) &&
                        !a.IsDeleted);

        if (branchId.HasValue)
            query = query.Where(a => a.BranchId == branchId.Value || (a.Student != null && a.Student.BranchId == branchId.Value));

        if (classGradeId.HasValue)
            query = query.Where(a => a.ClassGradeId == classGradeId.Value);

        if (classSectionId.HasValue)
            query = query.Where(a => a.ClassSectionId == classSectionId.Value);

        if (status.HasValue)
            query = query.Where(a => a.Status == status.Value);

        var records = await query
            .OrderBy(a => a.ClassGrade!.DisplayOrder)
            .ThenBy(a => a.Student!.LastName)
            .ThenBy(a => a.Student!.FirstName)
            .Select(a => new DetailedAttendanceRecordDto(
                a.Id,
                a.StudentId,
                a.Student != null ? a.Student.AdmissionNumber : string.Empty,
                a.Student != null ? a.Student.RollNumber : null,
                a.Student != null ? a.Student.FullName : "Student",
                a.ClassGradeId,
                a.ClassGrade != null ? a.ClassGrade.Name : "Class",
                a.ClassSectionId,
                a.ClassSection != null ? a.ClassSection.Name : null,
                a.Date,
                a.Status,
                a.Status.ToString(),
                a.Remarks,
                a.IsLocked,
                a.MarkedByStaff != null && a.MarkedByStaff.User != null ? a.MarkedByStaff.User.FullName : null
            ))
            .ToListAsync(cancellationToken);

        int total = records.Count;
        int present = records.Count(r => r.Status == AttendanceStatus.Present);
        int absent = records.Count(r => r.Status == AttendanceStatus.Absent);
        int late = records.Count(r => r.Status == AttendanceStatus.Late);
        int excused = records.Count(r => r.Status == AttendanceStatus.Excused);
        double pct = total > 0 ? Math.Round(((double)(present + late) / total) * 100, 1) : 100.0;

        var summary = new AttendanceSummaryDto(
            targetDate,
            total,
            present,
            absent,
            late,
            excused,
            pct,
            records
        );

        return ApiSuccess(summary);
    }

    // =========================================================================
    // 2. MONTHLY SUMMARY & DEFAULTERS
    // =========================================================================

    [HttpGet("summary")]
    [RequirePermission(Permissions.AttendanceView)]
    public async Task<IActionResult> GetMonthlySummary(
        [FromQuery] int? year,
        [FromQuery] int? month,
        [FromQuery] Guid? branchId,
        [FromQuery] Guid? classGradeId,
        [FromQuery] Guid? classSectionId,
        CancellationToken cancellationToken = default)
    {
        Guid targetOrgId = GetTargetOrgId();
        if (targetOrgId == Guid.Empty)
            return ApiError("Tenant context required.", StatusCodes.Status400BadRequest);

        if (CurrentUserService.RoleType == UserRoleType.Principal && CurrentUserService.BranchId.HasValue)
        {
            if (branchId.HasValue && branchId.Value != CurrentUserService.BranchId.Value)
                return ApiError("Unauthorized access to another branch.", StatusCodes.Status403Forbidden);

            branchId = CurrentUserService.BranchId.Value;
        }

        int targetYear = year ?? DateTime.UtcNow.Year;
        int targetMonth = month ?? DateTime.UtcNow.Month;
        var startDate = new DateTime(targetYear, targetMonth, 1);
        var endDate = startDate.AddMonths(1);

        var query = _context.AttendanceRecords
            .Include(a => a.Student)
            .Where(a => a.OrganizationId == targetOrgId &&
                        a.Date >= startDate && a.Date < endDate &&
                        !a.IsDeleted);

        if (branchId.HasValue)
            query = query.Where(a => a.BranchId == branchId.Value || (a.Student != null && a.Student.BranchId == branchId.Value));

        if (classGradeId.HasValue)
            query = query.Where(a => a.ClassGradeId == classGradeId.Value);

        if (classSectionId.HasValue)
            query = query.Where(a => a.ClassSectionId == classSectionId.Value);

        var records = await query.ToListAsync(cancellationToken);

        var groupedByStudent = records
            .GroupBy(a => a.StudentId)
            .ToList();

        var studentIds = groupedByStudent.Select(g => g.Key).ToList();
        var students = await _context.Students
            .Where(s => studentIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, cancellationToken);

        var studentRates = new List<StudentAttendanceRateDto>();
        foreach (var group in groupedByStudent)
        {
            if (!students.TryGetValue(group.Key, out var student)) continue;

            int total = group.Count();
            int present = group.Count(r => r.Status == AttendanceStatus.Present);
            int absent = group.Count(r => r.Status == AttendanceStatus.Absent);
            int late = group.Count(r => r.Status == AttendanceStatus.Late);
            int excused = group.Count(r => r.Status == AttendanceStatus.Excused);
            double pct = total > 0 ? Math.Round(((double)(present + late) / total) * 100, 1) : 100.0;

            studentRates.Add(new StudentAttendanceRateDto(
                student.Id,
                student.AdmissionNumber,
                student.RollNumber,
                student.FullName,
                student.CurrentGrade,
                student.Section,
                total,
                present,
                absent,
                late,
                excused,
                pct,
                pct < 75.0 // low attendance threshold
            ));
        }

        int workingDays = records.Select(r => r.Date.Date).Distinct().Count();
        double overallPct = studentRates.Count > 0 ? Math.Round(studentRates.Average(s => s.Percentage), 1) : 100.0;

        var result = new MonthlyAttendanceSummaryDto(
            targetYear,
            targetMonth,
            workingDays,
            overallPct,
            studentRates.OrderBy(s => s.Grade).ThenBy(s => s.FullName).ToList()
        );

        return ApiSuccess(result);
    }

    // =========================================================================
    // 3. ATTENDANCE LOCKING & UNLOCKING
    // =========================================================================

    [HttpPost("lock")]
    [RequirePermission(Permissions.AttendanceEdit)]
    public async Task<IActionResult> LockAttendance([FromBody] LockAttendanceRequestDto request, CancellationToken cancellationToken)
    {
        Guid targetOrgId = GetTargetOrgId();
        if (targetOrgId == Guid.Empty)
            return ApiError("Tenant context required.", StatusCodes.Status400BadRequest);

        var targetDate = request.Date.Date;

        var query = _context.AttendanceRecords
            .Where(a => a.OrganizationId == targetOrgId &&
                        a.ClassGradeId == request.ClassGradeId &&
                        a.Date >= targetDate && a.Date < targetDate.AddDays(1) &&
                        !a.IsDeleted);

        if (request.ClassSectionId.HasValue)
            query = query.Where(a => a.ClassSectionId == request.ClassSectionId.Value);

        var records = await query.ToListAsync(cancellationToken);
        if (records.Count == 0)
            return ApiError("No attendance records found to lock for the specified class and date.", StatusCodes.Status404NotFound);

        foreach (var r in records)
        {
            r.IsLocked = true;
        }

        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            AuditAction.Update, "Attendance", "LockAttendance", $"{request.ClassGradeId}:{targetDate:yyyyMMdd}",
            $"Locked attendance for class {request.ClassGradeId} on {targetDate:yyyy-MM-dd} ({records.Count} records)",
            organizationId: targetOrgId,
            branchId: records.FirstOrDefault()?.BranchId,
            userId: CurrentUserService.UserId,
            userEmail: CurrentUserService.Email,
            cancellationToken: cancellationToken);

        return ApiSuccess(new { lockedCount = records.Count }, $"Attendance locked successfully for {records.Count} student records.");
    }

    [HttpPost("unlock")]
    [RequirePermission(Permissions.AttendanceEdit)]
    public async Task<IActionResult> UnlockAttendance([FromBody] UnlockAttendanceRequestDto request, CancellationToken cancellationToken)
    {
        Guid targetOrgId = GetTargetOrgId();
        if (targetOrgId == Guid.Empty)
            return ApiError("Tenant context required.", StatusCodes.Status400BadRequest);

        var targetDate = request.Date.Date;

        var query = _context.AttendanceRecords
            .Where(a => a.OrganizationId == targetOrgId &&
                        a.ClassGradeId == request.ClassGradeId &&
                        a.Date >= targetDate && a.Date < targetDate.AddDays(1) &&
                        !a.IsDeleted);

        if (request.ClassSectionId.HasValue)
            query = query.Where(a => a.ClassSectionId == request.ClassSectionId.Value);

        var records = await query.ToListAsync(cancellationToken);
        if (records.Count == 0)
            return ApiError("No attendance records found to unlock for the specified class and date.", StatusCodes.Status404NotFound);

        foreach (var r in records)
        {
            r.IsLocked = false;
        }

        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            AuditAction.Update, "Attendance", "UnlockAttendance", $"{request.ClassGradeId}:{targetDate:yyyyMMdd}",
            $"Unlocked attendance for class {request.ClassGradeId} on {targetDate:yyyy-MM-dd} ({records.Count} records)",
            organizationId: targetOrgId,
            branchId: records.FirstOrDefault()?.BranchId,
            userId: CurrentUserService.UserId,
            userEmail: CurrentUserService.Email,
            cancellationToken: cancellationToken);

        return ApiSuccess(new { unlockedCount = records.Count }, $"Attendance unlocked successfully for {records.Count} student records.");
    }

    // =========================================================================
    // 4. ATTENDANCE CORRECTION WORKFLOW
    // =========================================================================

    [HttpPost("correct")]
    [RequirePermission(Permissions.AttendanceEdit)]
    public async Task<IActionResult> CorrectAttendance([FromBody] CorrectAttendanceRequestDto request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            return ApiError("A valid correction reason is required for administrative audit.", StatusCodes.Status400BadRequest);

        Guid targetOrgId = GetTargetOrgId();
        var record = await _context.AttendanceRecords
            .Include(a => a.Student)
            .FirstOrDefaultAsync(a => a.Id == request.RecordId && !a.IsDeleted, cancellationToken);

        if (record == null)
            return ApiError("Attendance record not found.", StatusCodes.Status404NotFound);

        if (!CurrentUserService.IsSuperAdmin && record.OrganizationId != targetOrgId)
            return ApiError("Unauthorized access to record outside your organization.", StatusCodes.Status403Forbidden);

        if (CurrentUserService.RoleType == UserRoleType.Principal && CurrentUserService.BranchId.HasValue && record.BranchId != CurrentUserService.BranchId.Value)
            return ApiError("Unauthorized access to another branch's record.", StatusCodes.Status403Forbidden);

        var oldStatus = record.Status;
        record.Status = request.NewStatus;
        record.Remarks = $"[Corrected from {oldStatus} by {CurrentUserService.Email}]: {request.Reason.Trim()}";
        record.UpdatedAtUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            AuditAction.Update, "Attendance", "AttendanceCorrection", record.Id.ToString(),
            $"Corrected attendance for student '{record.Student?.FullName}' on {record.Date:yyyy-MM-dd} from '{oldStatus}' to '{request.NewStatus}'. Reason: {request.Reason}",
            organizationId: record.OrganizationId,
            branchId: record.BranchId,
            userId: CurrentUserService.UserId,
            userEmail: CurrentUserService.Email,
            cancellationToken: cancellationToken);

        return ApiSuccess(new
        {
            recordId = record.Id,
            oldStatus = oldStatus.ToString(),
            newStatus = record.Status.ToString(),
            remarks = record.Remarks
        }, "Attendance correction recorded successfully.");
    }

    // =========================================================================
    // 5. ADMINISTRATIVE BULK MARKING
    // =========================================================================

    [HttpPost("bulk")]
    [RequirePermission(Permissions.AttendanceMark)]
    public async Task<IActionResult> BulkMarkAttendance([FromBody] AdminMarkAttendanceRequestDto request, CancellationToken cancellationToken)
    {
        Guid targetOrgId = GetTargetOrgId();
        if (targetOrgId == Guid.Empty)
            return ApiError("Tenant context required.", StatusCodes.Status400BadRequest);

        var classGrade = await _context.ClassGrades
            .FirstOrDefaultAsync(cg => cg.Id == request.ClassGradeId && cg.OrganizationId == targetOrgId && !cg.IsDeleted, cancellationToken);

        if (classGrade == null)
            return ApiError("Class/Grade not found.", StatusCodes.Status404NotFound);

        if (!classGrade.IsActive)
            return ApiError("Cannot mark attendance for an inactive class/grade.", StatusCodes.Status400BadRequest);

        if (CurrentUserService.RoleType == UserRoleType.Principal && CurrentUserService.BranchId.HasValue && classGrade.BranchId != CurrentUserService.BranchId.Value)
            return ApiError("Unauthorized access to another branch's class.", StatusCodes.Status403Forbidden);

        var targetDate = request.Date.Date;

        // Check if locked
        bool isLocked = await _context.AttendanceRecords.AnyAsync(a =>
            a.OrganizationId == targetOrgId &&
            a.ClassGradeId == request.ClassGradeId &&
            a.Date >= targetDate && a.Date < targetDate.AddDays(1) &&
            a.IsLocked &&
            !a.IsDeleted, cancellationToken);

        if (isLocked)
            return ApiError("Attendance for this class on this date is locked and cannot be updated without unlocking first.", StatusCodes.Status400BadRequest);

        int recorded = 0;
        var absentStudentIds = new List<Guid>();
        foreach (var entry in request.Entries)
        {
            var student = await _context.Students
                .FirstOrDefaultAsync(s => s.Id == entry.StudentId && s.OrganizationId == targetOrgId && !s.IsDeleted, cancellationToken);

            if (student == null) continue;

            if (entry.Status == AttendanceStatus.Absent)
            {
                absentStudentIds.Add(entry.StudentId);
            }

            var existing = await _context.AttendanceRecords
                .FirstOrDefaultAsync(a => a.OrganizationId == targetOrgId &&
                                          a.StudentId == entry.StudentId &&
                                          a.Date >= targetDate && a.Date < targetDate.AddDays(1) &&
                                          !a.IsDeleted, cancellationToken);

            if (existing != null)
            {
                existing.Status = entry.Status;
                existing.Remarks = entry.Remarks;
                existing.IsLocked = request.LockImmediately;
                existing.UpdatedAtUtc = DateTime.UtcNow;
            }
            else
            {
                var record = new AttendanceRecord
                {
                    Id = Guid.NewGuid(),
                    OrganizationId = targetOrgId,
                    BranchId = student.BranchId ?? classGrade.BranchId,
                    ClassGradeId = request.ClassGradeId,
                    ClassSectionId = request.ClassSectionId,
                    SubjectCourseId = request.SubjectCourseId,
                    StudentId = entry.StudentId,
                    Date = targetDate,
                    Status = entry.Status,
                    Remarks = entry.Remarks,
                    IsLocked = request.LockImmediately
                };
                _context.AttendanceRecords.Add(record);
            }
            recorded++;
        }

        await _context.SaveChangesAsync(cancellationToken);

        // Auto-trigger absence alert SMS to guardians
        if (_smsNotificationService != null)
        {
            foreach (var absentStudentId in absentStudentIds)
            {
                await _smsNotificationService.SendAbsenceAlertAsync(targetOrgId, absentStudentId, targetDate, cancellationToken);
            }
        }

        await _auditService.LogAsync(
            AuditAction.Create, "Attendance", "AdminBulkAttendance", $"{request.ClassGradeId}:{targetDate:yyyyMMdd}",
            $"Admin recorded attendance for {recorded} students in class '{classGrade.Name}' on {targetDate:yyyy-MM-dd}",
            organizationId: targetOrgId,
            branchId: classGrade.BranchId,
            userId: CurrentUserService.UserId,
            userEmail: CurrentUserService.Email,
            cancellationToken: cancellationToken);

        return ApiSuccess(new { count = recorded, date = targetDate }, $"Attendance recorded for {recorded} students.");
    }
}
