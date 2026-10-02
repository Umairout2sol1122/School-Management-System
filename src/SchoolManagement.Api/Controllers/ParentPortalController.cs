using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.Common.Models;
using SchoolManagement.Domain.Entities.Academics;
using SchoolManagement.Domain.Entities.Student;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Api.Controllers;

[ApiController]
[Route("api/parent")]
[Authorize]
public class ParentPortalController : BaseApiController
{
    private readonly IApplicationDbContext _context;
    private readonly IAuditService _auditService;

    public ParentPortalController(IApplicationDbContext context, IAuditService auditService)
    {
        _context = context;
        _auditService = auditService;
    }

    private async Task<Guardian?> ResolveCurrentGuardianAsync(CancellationToken cancellationToken)
    {
        var userId = CurrentUserService.UserId;
        if (!userId.HasValue) return null;

        var orgId = CurrentUserService.OrganizationId ?? Guid.Empty;

        return await _context.Guardians
            .Include(g => g.StudentGuardians)
                .ThenInclude(sg => sg.Student)
                    .ThenInclude(s => s!.Branch)
            .FirstOrDefaultAsync(g => g.UserId == userId.Value && g.OrganizationId == orgId && !g.IsDeleted, cancellationToken);
    }

    private async Task<(Guardian? Guardian, Student? Student, IActionResult? Error)> ValidateChildAccessAsync(Guid studentId, CancellationToken cancellationToken)
    {
        var guardian = await ResolveCurrentGuardianAsync(cancellationToken);
        if (guardian == null)
        {
            return (null, null, ApiError("No guardian profile linked to your user account.", StatusCodes.Status403Forbidden));
        }

        var studentGuardian = guardian.StudentGuardians
            .FirstOrDefault(sg => sg.StudentId == studentId && !sg.IsDeleted);

        if (studentGuardian == null || studentGuardian.Student == null || studentGuardian.Student.IsDeleted)
        {
            return (guardian, null, ApiError("Unauthorized access. You do not have an active guardian relationship with this student.", StatusCodes.Status403Forbidden));
        }

        var student = studentGuardian.Student;

        if (!CurrentUserService.IsSuperAdmin && student.OrganizationId != CurrentUserService.OrganizationId)
        {
            return (guardian, null, ApiError("Cross-organization student access is forbidden.", StatusCodes.Status403Forbidden));
        }

        return (guardian, student, null);
    }

    // =========================================================================
    // 1. CHILDREN LIST & PROFILE
    // =========================================================================

    [HttpGet("children")]
    public async Task<IActionResult> GetChildren(CancellationToken cancellationToken)
    {
        var guardian = await ResolveCurrentGuardianAsync(cancellationToken);
        if (guardian == null)
            return ApiError("No guardian profile linked to your user account.", StatusCodes.Status403Forbidden);

        var children = guardian.StudentGuardians
            .Where(sg => !sg.IsDeleted && sg.Student != null && !sg.Student.IsDeleted)
            .Select(sg => new ParentChildSummaryDto(
                sg.Student!.Id,
                sg.Student.FullName,
                sg.Student.AdmissionNumber,
                sg.Student.RollNumber,
                sg.Student.CurrentGrade,
                sg.Student.Section,
                sg.Student.Status.ToString(),
                sg.Student.BranchId ?? Guid.Empty,
                sg.Student.Branch?.Name,
                sg.Relationship,
                sg.IsPrimaryContact
            ))
            .ToList();

        return ApiSuccess(children);
    }

    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile(CancellationToken cancellationToken)
    {
        var guardian = await ResolveCurrentGuardianAsync(cancellationToken);
        if (guardian == null)
            return ApiError("No guardian profile linked to your user account.", StatusCodes.Status403Forbidden);

        var children = guardian.StudentGuardians
            .Where(sg => !sg.IsDeleted && sg.Student != null && !sg.Student.IsDeleted)
            .Select(sg => new ParentChildSummaryDto(
                sg.Student!.Id,
                sg.Student.FullName,
                sg.Student.AdmissionNumber,
                sg.Student.RollNumber,
                sg.Student.CurrentGrade,
                sg.Student.Section,
                sg.Student.Status.ToString(),
                sg.Student.BranchId ?? Guid.Empty,
                sg.Student.Branch?.Name,
                sg.Relationship,
                sg.IsPrimaryContact
            ))
            .ToList();

        var profile = new ParentGuardianProfileDto(
            guardian.Id,
            guardian.UserId,
            guardian.FullName,
            guardian.Phone,
            guardian.Email,
            guardian.AlternatePhone,
            guardian.Occupation,
            guardian.Address,
            guardian.City,
            guardian.IsEmergencyContact,
            children
        );

        return ApiSuccess(profile);
    }

    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateParentContactRequestDto request, CancellationToken cancellationToken)
    {
        var guardian = await ResolveCurrentGuardianAsync(cancellationToken);
        if (guardian == null)
            return ApiError("No guardian profile linked to your user account.", StatusCodes.Status403Forbidden);

        if (!string.IsNullOrWhiteSpace(request.Phone))
            guardian.Phone = request.Phone.Trim();

        if (request.AlternatePhone != null)
            guardian.AlternatePhone = request.AlternatePhone.Trim();

        if (request.Address != null)
            guardian.Address = request.Address.Trim();

        if (request.City != null)
            guardian.City = request.City.Trim();

        if (request.Occupation != null)
            guardian.Occupation = request.Occupation.Trim();

        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            AuditAction.Update, "Guardians", "GuardianProfile", guardian.Id.ToString(),
            $"Parent '{guardian.FullName}' updated personal contact details",
            organizationId: guardian.OrganizationId,
            branchId: guardian.BranchId,
            userId: CurrentUserService.UserId,
            userEmail: CurrentUserService.Email,
            cancellationToken: cancellationToken);

        return ApiSuccess("Profile updated successfully.");
    }

    // =========================================================================
    // 2. PARENT DASHBOARD (SWITCHABLE PER CHILD)
    // =========================================================================

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard([FromQuery] Guid? studentId, CancellationToken cancellationToken)
    {
        var guardian = await ResolveCurrentGuardianAsync(cancellationToken);
        if (guardian == null)
            return ApiError("No guardian profile linked to your user account.", StatusCodes.Status403Forbidden);

        var validGuardians = guardian.StudentGuardians
            .Where(sg => !sg.IsDeleted && sg.Student != null && !sg.Student.IsDeleted)
            .ToList();

        var children = validGuardians
            .Select(sg => new ParentChildSummaryDto(
                sg.Student!.Id,
                sg.Student.FullName,
                sg.Student.AdmissionNumber,
                sg.Student.RollNumber,
                sg.Student.CurrentGrade,
                sg.Student.Section,
                sg.Student.Status.ToString(),
                sg.Student.BranchId ?? Guid.Empty,
                sg.Student.Branch?.Name,
                sg.Relationship,
                sg.IsPrimaryContact
            ))
            .ToList();

        StudentGuardian? selectedLink = null;
        if (studentId.HasValue)
        {
            selectedLink = validGuardians.FirstOrDefault(sg => sg.StudentId == studentId.Value);
            if (selectedLink == null)
                return ApiError("Unauthorized: You do not have guardian relationship with this student.", StatusCodes.Status403Forbidden);
        }
        else
        {
            selectedLink = validGuardians.OrderByDescending(sg => sg.IsPrimaryContact).FirstOrDefault();
        }

        if (selectedLink == null || selectedLink.Student == null)
        {
            return ApiSuccess(new ParentDashboardDto(
                null,
                children,
                0, 0, 0, 0, 0, 0, 0,
                Array.Empty<StudentAnnouncementDto>()
            ));
        }

        var child = selectedLink.Student;
        var orgId = CurrentUserService.OrganizationId ?? Guid.Empty;

        // Attendance stats
        var attendanceRecords = await _context.AttendanceRecords
            .Where(a => a.OrganizationId == orgId && a.StudentId == child.Id && !a.IsDeleted)
            .ToListAsync(cancellationToken);

        int totalDays = attendanceRecords.Count;
        int presentDays = attendanceRecords.Count(a => a.Status == AttendanceStatus.Present || a.Status == AttendanceStatus.Late);
        int absentDays = attendanceRecords.Count(a => a.Status == AttendanceStatus.Absent);
        double attPct = totalDays > 0 ? Math.Round(((double)presentDays / totalDays) * 100, 1) : 100.0;

        // Outstanding fees
        var pendingFeeInvoices = await _context.FeeInvoices
            .Where(fi => fi.OrganizationId == orgId && fi.StudentId == child.Id && !fi.IsDeleted && fi.Status != FeeStatus.Paid)
            .ToListAsync(cancellationToken);

        decimal outstandingFees = pendingFeeInvoices.Sum(fi => fi.TotalAmount - fi.PaidAmount);

        // Grade reference for class-based queries
        var classGrade = await _context.ClassGrades
            .FirstOrDefaultAsync(c => c.OrganizationId == orgId && c.Name == child.CurrentGrade && !c.IsDeleted, cancellationToken);
        var classGradeId = classGrade?.Id;

        // Upcoming exams
        var now = DateTime.UtcNow;
        int upcomingExams = 0;
        if (classGradeId.HasValue)
        {
            upcomingExams = await _context.ExamSchedules
                .CountAsync(e => e.OrganizationId == orgId && e.ClassGradeId == classGradeId.Value && e.ExamDate >= now.Date && !e.IsDeleted, cancellationToken);
        }

        // Active assignments
        int activeAssignments = 0;
        if (classGradeId.HasValue)
        {
            activeAssignments = await _context.HomeworkAssignments
                .CountAsync(a => a.OrganizationId == orgId && a.ClassGradeId == classGradeId.Value && a.DueDate >= now && !a.IsDeleted, cancellationToken);
        }

        // Announcements
        var announcements = await _context.Announcements
            .Where(a => a.OrganizationId == orgId && !a.IsDeleted &&
                        (a.TargetAudience == "All" || a.TargetAudience == "Parents") &&
                        (!a.BranchId.HasValue || a.BranchId == child.BranchId))
            .OrderByDescending(a => a.PublishDate)
            .Take(5)
            .Select(a => new StudentAnnouncementDto(
                a.Id,
                a.Title,
                a.Content,
                a.Priority,
                a.PublishDate
            ))
            .ToListAsync(cancellationToken);

        var selectedChildDto = new ParentChildSummaryDto(
            child.Id,
            child.FullName,
            child.AdmissionNumber,
            child.RollNumber,
            child.CurrentGrade,
            child.Section,
            child.Status.ToString(),
            child.BranchId ?? Guid.Empty,
            child.Branch?.Name,
            selectedLink.Relationship,
            selectedLink.IsPrimaryContact
        );

        var dashboard = new ParentDashboardDto(
            selectedChildDto,
            children,
            attPct,
            totalDays,
            presentDays,
            absentDays,
            outstandingFees,
            upcomingExams,
            activeAssignments,
            announcements
        );

        return ApiSuccess(dashboard);
    }

    // =========================================================================
    // 3. CHILD ATTENDANCE
    // =========================================================================

    [HttpGet("children/{studentId:guid}/attendance")]
    public async Task<IActionResult> GetChildAttendance(Guid studentId, [FromQuery] int? month, [FromQuery] int? year, CancellationToken cancellationToken)
    {
        var (_, student, error) = await ValidateChildAccessAsync(studentId, cancellationToken);
        if (error != null) return error;

        var query = _context.AttendanceRecords
            .Where(a => a.StudentId == studentId && !a.IsDeleted);

        if (year.HasValue)
        {
            query = query.Where(a => a.Date.Year == year.Value);
        }
        if (month.HasValue)
        {
            query = query.Where(a => a.Date.Month == month.Value);
        }

        var records = await query
            .OrderByDescending(a => a.Date)
            .Select(a => new StudentAttendanceDto(
                a.Date,
                a.Status,
                a.Status.ToString(),
                null,
                a.Remarks
            ))
            .ToListAsync(cancellationToken);

        int total = records.Count;
        int present = records.Count(r => r.Status == AttendanceStatus.Present || r.Status == AttendanceStatus.Late);
        int absent = records.Count(r => r.Status == AttendanceStatus.Absent);
        int late = records.Count(r => r.Status == AttendanceStatus.Late);
        int leave = records.Count(r => r.Status == AttendanceStatus.Excused);
        double percentage = total > 0 ? Math.Round(((double)present / total) * 100, 1) : 100.0;

        return ApiSuccess(new
        {
            studentId,
            studentName = student!.FullName,
            totalDays = total,
            presentDays = present,
            absentDays = absent,
            lateDays = late,
            leaveDays = leave,
            attendancePercentage = percentage,
            records
        });
    }

    // =========================================================================
    // 4. CHILD TIMETABLE
    // =========================================================================

    [HttpGet("children/{studentId:guid}/timetable")]
    public async Task<IActionResult> GetChildTimetable(Guid studentId, CancellationToken cancellationToken)
    {
        var (_, student, error) = await ValidateChildAccessAsync(studentId, cancellationToken);
        if (error != null) return error;

        var classGrade = await _context.ClassGrades
            .FirstOrDefaultAsync(c => c.OrganizationId == student!.OrganizationId && c.Name == student.CurrentGrade && !c.IsDeleted, cancellationToken);

        if (classGrade == null)
        {
            return ApiSuccess(new
            {
                studentId,
                studentName = student!.FullName,
                grade = student.CurrentGrade,
                section = student.Section,
                schedule = new List<StudentTimetableItemDto>()
            });
        }

        var timetable = await _context.TimetableSlots
            .Include(t => t.SubjectCourse)
            .Include(t => t.TeacherStaff).ThenInclude(ts => ts!.User)
            .Where(t => t.OrganizationId == student!.OrganizationId &&
                        t.ClassGradeId == classGrade.Id &&
                        (!student.BranchId.HasValue || t.BranchId == student.BranchId.Value) &&
                        t.IsActive &&
                        !t.IsDeleted)
            .OrderBy(t => t.DayOfWeek)
            .ThenBy(t => t.StartTime)
            .Select(t => new StudentTimetableItemDto(
                t.DayOfWeek,
                t.DayOfWeek.ToString(),
                t.StartTime,
                t.EndTime,
                t.SubjectCourse != null ? t.SubjectCourse.Name : "Subject",
                t.TeacherStaff != null && t.TeacherStaff.User != null ? t.TeacherStaff.User.FullName : "Instructor",
                t.RoomNumber
            ))
            .ToListAsync(cancellationToken);

        return ApiSuccess(new
        {
            studentId,
            studentName = student!.FullName,
            grade = student.CurrentGrade,
            section = student.Section,
            schedule = timetable
        });
    }

    // =========================================================================
    // 5. CHILD ASSIGNMENTS / HOMEWORK
    // =========================================================================

    [HttpGet("children/{studentId:guid}/assignments")]
    public async Task<IActionResult> GetChildAssignments(Guid studentId, CancellationToken cancellationToken)
    {
        var (_, student, error) = await ValidateChildAccessAsync(studentId, cancellationToken);
        if (error != null) return error;

        var classGrade = await _context.ClassGrades
            .FirstOrDefaultAsync(c => c.OrganizationId == student!.OrganizationId && c.Name == student.CurrentGrade && !c.IsDeleted, cancellationToken);

        if (classGrade == null)
            return ApiSuccess(new List<StudentAssignmentItemDto>());

        var assignments = await _context.HomeworkAssignments
            .Include(a => a.SubjectCourse)
            .Include(a => a.TeacherUser)
            .Where(a => a.OrganizationId == student!.OrganizationId &&
                        a.ClassGradeId == classGrade.Id &&
                        (!student.BranchId.HasValue || a.BranchId == student.BranchId.Value) &&
                        !a.IsDeleted)
            .OrderByDescending(a => a.DueDate)
            .ToListAsync(cancellationToken);

        var submissions = await _context.AssignmentSubmissions
            .Where(s => s.StudentId == studentId && !s.IsDeleted)
            .ToDictionaryAsync(s => s.HomeworkAssignmentId, cancellationToken);

        var items = assignments.Select(a =>
        {
            submissions.TryGetValue(a.Id, out var sub);
            return new StudentAssignmentItemDto(
                a.Id,
                a.Title,
                a.Description,
                a.SubjectCourse?.Name ?? "Subject",
                a.TeacherUser?.FullName ?? "Instructor",
                a.DueDate,
                a.MaxMarks,
                a.AttachmentUrl,
                a.AttachmentFileName,
                a.DueDate < DateTime.UtcNow ? "Past Due" : "Active",
                sub != null,
                sub?.Status,
                sub?.MarksAwarded,
                sub?.Feedback
            );
        }).ToList();

        return ApiSuccess(items);
    }

    // =========================================================================
    // 6. CHILD EXAMS & PUBLISHED RESULTS ONLY
    // =========================================================================

    [HttpGet("children/{studentId:guid}/exams")]
    public async Task<IActionResult> GetChildExams(Guid studentId, CancellationToken cancellationToken)
    {
        var (_, student, error) = await ValidateChildAccessAsync(studentId, cancellationToken);
        if (error != null) return error;

        var classGrade = await _context.ClassGrades
            .FirstOrDefaultAsync(c => c.OrganizationId == student!.OrganizationId && c.Name == student.CurrentGrade && !c.IsDeleted, cancellationToken);

        var exams = new List<StudentExamItemDto>();
        if (classGrade != null)
        {
            exams = await _context.ExamSchedules
                .Include(e => e.SubjectCourse)
                .Where(e => e.OrganizationId == student!.OrganizationId &&
                            e.ClassGradeId == classGrade.Id &&
                            (!student.BranchId.HasValue || e.BranchId == student.BranchId.Value) &&
                            !e.IsDeleted)
                .OrderBy(e => e.ExamDate)
                .Select(e => new StudentExamItemDto(
                    e.Id,
                    e.Title,
                    e.Term,
                    e.SubjectCourse != null ? e.SubjectCourse.Name : "Subject",
                    e.ExamDate,
                    e.StartTime,
                    e.EndTime,
                    e.RoomNumber,
                    e.TotalMarks,
                    e.PassingMarks
                ))
                .ToListAsync(cancellationToken);
        }

        // STRICT SECURITY: Never leak draft or unpublished exam results to parents!
        var results = await _context.ExamResults
            .Include(r => r.ExamSchedule).ThenInclude(es => es!.SubjectCourse)
            .Where(r => r.OrganizationId == student!.OrganizationId &&
                        r.StudentId == studentId &&
                        r.IsFinalized &&
                        r.ExamSchedule != null &&
                        r.ExamSchedule.IsPublished &&
                        !r.IsDeleted)
            .OrderByDescending(r => r.ExamSchedule!.ExamDate)
            .Select(r => new StudentResultItemDto(
                r.ExamSchedule != null ? r.ExamSchedule.Title : "Exam",
                r.ExamSchedule != null ? r.ExamSchedule.Term : "Term",
                r.ExamSchedule != null && r.ExamSchedule.SubjectCourse != null ? r.ExamSchedule.SubjectCourse.Name : "Subject",
                r.ExamSchedule != null ? r.ExamSchedule.ExamDate : DateTime.MinValue,
                r.MarksObtained,
                r.ExamSchedule != null ? r.ExamSchedule.TotalMarks : 100,
                r.Percentage,
                r.Grade,
                r.Gpa,
                r.Remarks
            ))
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        return ApiSuccess(new
        {
            studentId,
            studentName = student!.FullName,
            grade = student.CurrentGrade,
            upcomingExams = exams.Where(e => e.ExamDate >= now.Date).ToList(),
            pastExams = exams.Where(e => e.ExamDate < now.Date).ToList(),
            publishedResults = results
        });
    }

    // =========================================================================
    // 7. CHILD FEES & PAYMENTS
    // =========================================================================

    [HttpGet("children/{studentId:guid}/fees")]
    public async Task<IActionResult> GetChildFees(Guid studentId, CancellationToken cancellationToken)
    {
        var (_, student, error) = await ValidateChildAccessAsync(studentId, cancellationToken);
        if (error != null) return error;

        var invoices = await _context.FeeInvoices
            .Include(f => f.Payments)
            .Where(f => f.OrganizationId == student!.OrganizationId && f.StudentId == studentId && !f.IsDeleted)
            .OrderByDescending(f => f.IssueDate)
            .ToListAsync(cancellationToken);

        decimal totalBilled = invoices.Sum(f => f.TotalAmount);
        decimal totalPaid = invoices.Sum(f => f.PaidAmount);
        decimal totalBalance = totalBilled - totalPaid;

        var dtos = invoices.Select(f => new StudentFeeInvoiceDto(
            f.Id,
            f.InvoiceNumber,
            f.Title,
            f.IssueDate,
            f.DueDate,
            f.TotalAmount,
            f.PaidAmount,
            f.TotalAmount - f.PaidAmount,
            f.Status,
            f.Status.ToString(),
            f.Payments.Where(p => !p.IsDeleted).Select(p => new FeePaymentDto(
                p.Id,
                p.ReceiptNumber,
                p.AmountPaid,
                p.PaymentDate,
                p.PaymentMethod.ToString(),
                p.TransactionReference
            )).ToList()
        )).ToList();

        return ApiSuccess(new
        {
            studentId,
            studentName = student!.FullName,
            totalBilled,
            totalPaid,
            totalBalance,
            invoices = dtos
        });
    }

    // =========================================================================
    // 8. CHILD DOCUMENTS
    // =========================================================================

    [HttpGet("children/{studentId:guid}/documents")]
    public async Task<IActionResult> GetChildDocuments(Guid studentId, CancellationToken cancellationToken)
    {
        var (_, student, error) = await ValidateChildAccessAsync(studentId, cancellationToken);
        if (error != null) return error;

        var documents = await _context.StudentDocuments
            .Where(d => d.OrganizationId == student!.OrganizationId && d.StudentId == studentId && !d.IsDeleted)
            .OrderByDescending(d => d.CreatedAtUtc)
            .Select(d => new StudentDocumentDto(
                d.Id,
                d.Title,
                d.DocumentType,
                d.DocumentType.ToString(),
                d.FileName,
                d.FileUrl,
                d.FileSizeBytes,
                d.Description,
                d.CreatedAtUtc
            ))
            .ToListAsync(cancellationToken);

        return ApiSuccess(documents);
    }

    // =========================================================================
    // 9. ANNOUNCEMENTS
    // =========================================================================

    [HttpGet("announcements")]
    public async Task<IActionResult> GetAnnouncements(CancellationToken cancellationToken)
    {
        var guardian = await ResolveCurrentGuardianAsync(cancellationToken);
        if (guardian == null)
            return ApiError("No guardian profile linked to your user account.", StatusCodes.Status403Forbidden);

        var orgId = CurrentUserService.OrganizationId ?? Guid.Empty;

        var childBranchIds = guardian.StudentGuardians
            .Where(sg => !sg.IsDeleted && sg.Student != null && !sg.Student.IsDeleted && sg.Student.BranchId.HasValue)
            .Select(sg => sg.Student!.BranchId!.Value)
            .Distinct()
            .ToList();

        var announcements = await _context.Announcements
            .Where(a => a.OrganizationId == orgId &&
                        !a.IsDeleted &&
                        (a.TargetAudience == "All" || a.TargetAudience == "Parents") &&
                        (!a.BranchId.HasValue || childBranchIds.Contains(a.BranchId.Value)))
            .OrderByDescending(a => a.PublishDate)
            .Take(30)
            .Select(a => new StudentAnnouncementDto(
                a.Id,
                a.Title,
                a.Content,
                a.Priority,
                a.PublishDate
            ))
            .ToListAsync(cancellationToken);

        return ApiSuccess(announcements);
    }
}
