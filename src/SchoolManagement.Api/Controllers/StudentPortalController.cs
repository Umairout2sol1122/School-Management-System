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
[Route("api/student")]
[Authorize]
public class StudentPortalController : BaseApiController
{
    private readonly IApplicationDbContext _context;
    private readonly IAuditService _auditService;

    public StudentPortalController(IApplicationDbContext context, IAuditService auditService)
    {
        _context = context;
        _auditService = auditService;
    }

    private Guid GetTargetOrgId()
    {
        return CurrentUserService.OrganizationId ?? Guid.Empty;
    }

    private async Task<Student?> ResolveCurrentStudentAsync(CancellationToken cancellationToken, Guid? explicitStudentId = null)
    {
        Guid orgId = GetTargetOrgId();
        if (orgId == Guid.Empty) return null;

        var userId = CurrentUserService.UserId;

        // If SuperAdmin or OrgAdmin is inspecting a student record explicitly
        if ((CurrentUserService.IsSuperAdmin || CurrentUserService.RoleType == UserRoleType.OrganizationAdmin) && explicitStudentId.HasValue)
        {
            return await _context.Students
                .Include(s => s.Branch)
                .Include(s => s.StudentGuardians).ThenInclude(sg => sg.Guardian)
                .FirstOrDefaultAsync(s => s.OrganizationId == orgId && s.Id == explicitStudentId.Value && !s.IsDeleted, cancellationToken);
        }

        // Standard student: STRICTLY resolve via current user identity
        return await _context.Students
            .Include(s => s.Branch)
            .Include(s => s.StudentGuardians).ThenInclude(sg => sg.Guardian)
            .FirstOrDefaultAsync(s => s.OrganizationId == orgId && s.UserId == userId && !s.IsDeleted, cancellationToken);
    }

    // =========================================================================
    // 1. STUDENT PROFILE
    // =========================================================================

    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile([FromQuery] Guid? studentId, CancellationToken cancellationToken)
    {
        var student = await ResolveCurrentStudentAsync(cancellationToken, studentId);
        if (student == null) return ApiNotFound("Student record not found.");

        var primaryGuardian = student.StudentGuardians
            .OrderByDescending(sg => sg.IsPrimaryContact)
            .Select(sg => sg.Guardian)
            .FirstOrDefault();

        var relType = student.StudentGuardians
            .OrderByDescending(sg => sg.IsPrimaryContact)
            .Select(sg => sg.Relationship.ToString())
            .FirstOrDefault() ?? "Guardian";

        var profile = new StudentProfileDto(
            student.Id,
            student.UserId,
            student.AdmissionNumber,
            student.RollNumber,
            student.FirstName,
            student.LastName,
            student.FullName,
            student.DateOfBirth,
            student.Gender.ToString(),
            student.BloodGroup,
            student.CurrentGrade,
            student.Section,
            student.AdmissionDate,
            student.Status.ToString(),
            student.EmergencyContactName,
            student.EmergencyContactPhone,
            student.Address,
            student.City,
            primaryGuardian != null ? $"{primaryGuardian.FirstName} {primaryGuardian.LastName}".Trim() : null,
            primaryGuardian?.Phone,
            relType
        );

        return ApiSuccess(profile);
    }

    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateStudentContactRequest request, CancellationToken cancellationToken)
    {
        var student = await ResolveCurrentStudentAsync(cancellationToken);
        if (student == null) return ApiNotFound("Student record not found.");

        // Only allow demographic/emergency contact edits (academic fields protected)
        if (request.EmergencyContactName != null) student.EmergencyContactName = request.EmergencyContactName.Trim();
        if (request.EmergencyContactPhone != null) student.EmergencyContactPhone = request.EmergencyContactPhone.Trim();
        if (request.Address != null) student.Address = request.Address.Trim();
        if (request.City != null) student.City = request.City.Trim();

        await _context.SaveChangesAsync(cancellationToken);
        return ApiSuccess("Student contact information updated successfully.");
    }

    // =========================================================================
    // 2. STUDENT DASHBOARD
    // =========================================================================

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard([FromQuery] Guid? studentId, CancellationToken cancellationToken)
    {
        var student = await ResolveCurrentStudentAsync(cancellationToken, studentId);
        if (student == null) return ApiNotFound("Student record not found.");

        Guid orgId = student.OrganizationId;

        // Attendance stats
        var attendanceRecords = await _context.AttendanceRecords
            .Where(a => a.OrganizationId == orgId && a.StudentId == student.Id && !a.IsDeleted)
            .ToListAsync(cancellationToken);

        int totalDays = attendanceRecords.Count;
        int presentDays = attendanceRecords.Count(a => a.Status == AttendanceStatus.Present || a.Status == AttendanceStatus.Late);
        int absentDays = attendanceRecords.Count(a => a.Status == AttendanceStatus.Absent);
        double attendancePct = totalDays > 0 ? Math.Round(((double)presentDays / totalDays) * 100, 1) : 100.0;

        // Pending assignments
        var classGrade = await _context.ClassGrades.FirstOrDefaultAsync(cg => cg.OrganizationId == orgId && cg.Name == student.CurrentGrade && !cg.IsDeleted, cancellationToken);
        Guid classGradeId = classGrade?.Id ?? Guid.Empty;

        var mySubmittedIds = await _context.AssignmentSubmissions
            .Where(s => s.StudentId == student.Id && !s.IsDeleted)
            .Select(s => s.HomeworkAssignmentId)
            .ToListAsync(cancellationToken);

        int pendingAssignments = await _context.HomeworkAssignments
            .Where(h => h.OrganizationId == orgId &&
                        h.ClassGradeId == classGradeId &&
                        h.Status == AssignmentStatus.Active &&
                        h.DueDate >= DateTime.UtcNow &&
                        !mySubmittedIds.Contains(h.Id) &&
                        !h.IsDeleted)
            .CountAsync(cancellationToken);

        // Upcoming exams
        int upcomingExams = await _context.ExamSchedules
            .Where(e => e.OrganizationId == orgId &&
                        e.ClassGradeId == classGradeId &&
                        e.ExamDate >= DateTime.UtcNow.Date &&
                        !e.IsDeleted)
            .CountAsync(cancellationToken);

        // Unpaid fees
        decimal unpaidFees = await _context.FeeInvoices
            .Where(f => f.OrganizationId == orgId && f.StudentId == student.Id && f.Status != FeeStatus.Paid && !f.IsDeleted)
            .SumAsync(f => f.TotalAmount - f.PaidAmount, cancellationToken);

        // Today's timetable
        var todayDow = DateTime.UtcNow.DayOfWeek;
        var todaySchedule = await _context.TimetableSlots
            .Include(t => t.SubjectCourse)
            .Include(t => t.TeacherStaff).ThenInclude(ts => ts!.User)
            .Where(t => t.OrganizationId == orgId &&
                        t.ClassGradeId == classGradeId &&
                        t.DayOfWeek == todayDow &&
                        t.IsActive &&
                        !t.IsDeleted)
            .OrderBy(t => t.StartTime)
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

        // Recent announcements
        var announcements = await _context.Announcements
            .Where(a => a.OrganizationId == orgId &&
                        !a.IsDeleted &&
                        (a.TargetAudience == "All" || a.TargetAudience == "Students"))
            .OrderByDescending(a => a.PublishDate)
            .Take(5)
            .Select(a => new StudentAnnouncementDto(a.Id, a.Title, a.Content, a.Priority, a.PublishDate))
            .ToListAsync(cancellationToken);

        var dashboard = new StudentDashboardDto(
            student.FullName,
            student.AdmissionNumber,
            student.RollNumber,
            student.CurrentGrade,
            student.Section,
            student.Branch?.Name ?? "Main Campus",
            attendancePct,
            presentDays,
            absentDays,
            totalDays,
            pendingAssignments,
            upcomingExams,
            unpaidFees,
            todaySchedule,
            announcements
        );

        return ApiSuccess(dashboard);
    }

    // =========================================================================
    // 3. STUDENT CLASSES & SUBJECTS
    // =========================================================================

    [HttpGet("classes")]
    public async Task<IActionResult> GetClasses([FromQuery] Guid? studentId, CancellationToken cancellationToken)
    {
        var student = await ResolveCurrentStudentAsync(cancellationToken, studentId);
        if (student == null) return ApiNotFound("Student record not found.");

        var classGrade = await _context.ClassGrades
            .Include(c => c.Department)
            .Include(c => c.Sections)
            .FirstOrDefaultAsync(c => c.OrganizationId == student.OrganizationId && c.Name == student.CurrentGrade && !c.IsDeleted, cancellationToken);

        return ApiSuccess(new {
            CurrentGrade = student.CurrentGrade,
            Section = student.Section,
            Department = classGrade?.Department?.Name,
            ClassCode = classGrade?.Code,
            Branch = student.Branch?.Name ?? "Main Campus"
        });
    }

    [HttpGet("subjects")]
    public async Task<IActionResult> GetSubjects([FromQuery] Guid? studentId, CancellationToken cancellationToken)
    {
        var student = await ResolveCurrentStudentAsync(cancellationToken, studentId);
        if (student == null) return ApiNotFound("Student record not found.");

        var classGrade = await _context.ClassGrades
            .FirstOrDefaultAsync(c => c.OrganizationId == student.OrganizationId && c.Name == student.CurrentGrade && !c.IsDeleted, cancellationToken);

        if (classGrade == null)
            return ApiSuccess(new List<object>());

        var assignments = await _context.CourseAssignments
            .Include(ca => ca.SubjectCourse)
            .Include(ca => ca.TeacherUser)
            .Include(ca => ca.ClassSection)
            .Where(ca => ca.OrganizationId == student.OrganizationId &&
                         ca.ClassSection != null &&
                         ca.ClassSection.ClassGradeId == classGrade.Id &&
                         ca.IsActive &&
                         !ca.IsDeleted)
            .ToListAsync(cancellationToken);

        var subjects = assignments
            .Where(ca => ca.SubjectCourse != null)
            .Select(ca => new {
                SubjectId = ca.SubjectCourse!.Id,
                Name = ca.SubjectCourse.Name,
                Code = ca.SubjectCourse.Code,
                CreditHours = ca.SubjectCourse.CreditHours,
                TeacherName = ca.TeacherUser != null ? ca.TeacherUser.FullName : "TBA",
                Section = ca.ClassSection?.Name
            })
            .Distinct()
            .ToList();

        return ApiSuccess(subjects);
    }

    // =========================================================================
    // 4. STUDENT ATTENDANCE (READ-ONLY)
    // =========================================================================

    [HttpGet("attendance")]
    public async Task<IActionResult> GetAttendance([FromQuery] Guid? studentId, [FromQuery] int? month, [FromQuery] int? year, CancellationToken cancellationToken)
    {
        var student = await ResolveCurrentStudentAsync(cancellationToken, studentId);
        if (student == null) return ApiNotFound("Student record not found.");

        var query = _context.AttendanceRecords
            .Include(a => a.SubjectCourse)
            .Where(a => a.OrganizationId == student.OrganizationId && a.StudentId == student.Id && !a.IsDeleted);

        if (month.HasValue && year.HasValue)
        {
            var start = new DateTime(year.Value, month.Value, 1);
            var end = start.AddMonths(1);
            query = query.Where(a => a.Date >= start && a.Date < end);
        }

        var records = await query
            .OrderByDescending(a => a.Date)
            .Select(a => new StudentAttendanceDto(
                a.Date,
                a.Status,
                a.Status.ToString(),
                a.SubjectCourse != null ? a.SubjectCourse.Name : null,
                a.Remarks
            ))
            .ToListAsync(cancellationToken);

        int total = records.Count;
        int present = records.Count(r => r.Status == AttendanceStatus.Present || r.Status == AttendanceStatus.Late);
        double pct = total > 0 ? Math.Round(((double)present / total) * 100, 1) : 100.0;

        return ApiSuccess(new {
            AttendancePercentage = pct,
            TotalDays = total,
            PresentDays = present,
            AbsentDays = records.Count(r => r.Status == AttendanceStatus.Absent),
            Records = records
        });
    }

    // =========================================================================
    // 5. STUDENT TIMETABLE
    // =========================================================================

    [HttpGet("timetable")]
    public async Task<IActionResult> GetTimetable([FromQuery] Guid? studentId, CancellationToken cancellationToken)
    {
        var student = await ResolveCurrentStudentAsync(cancellationToken, studentId);
        if (student == null) return ApiNotFound("Student record not found.");

        var classGrade = await _context.ClassGrades.FirstOrDefaultAsync(c => c.OrganizationId == student.OrganizationId && c.Name == student.CurrentGrade && !c.IsDeleted, cancellationToken);
        if (classGrade == null) return ApiSuccess(new List<StudentTimetableItemDto>());

        var slots = await _context.TimetableSlots
            .Include(t => t.SubjectCourse)
            .Include(t => t.TeacherStaff).ThenInclude(ts => ts!.User)
            .Where(t => t.OrganizationId == student.OrganizationId &&
                        t.ClassGradeId == classGrade.Id &&
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

        return ApiSuccess(slots);
    }

    // =========================================================================
    // 6. STUDENT ASSIGNMENTS & SUBMISSIONS
    // =========================================================================

    [HttpGet("assignments")]
    public async Task<IActionResult> GetAssignments([FromQuery] Guid? studentId, CancellationToken cancellationToken)
    {
        var student = await ResolveCurrentStudentAsync(cancellationToken, studentId);
        if (student == null) return ApiNotFound("Student record not found.");

        var classGrade = await _context.ClassGrades.FirstOrDefaultAsync(c => c.OrganizationId == student.OrganizationId && c.Name == student.CurrentGrade && !c.IsDeleted, cancellationToken);
        if (classGrade == null) return ApiSuccess(new List<StudentAssignmentItemDto>());

        var assignments = await _context.HomeworkAssignments
            .Include(h => h.SubjectCourse)
            .Include(h => h.TeacherUser)
            .Where(h => h.OrganizationId == student.OrganizationId &&
                        h.ClassGradeId == classGrade.Id &&
                        h.Status != AssignmentStatus.Cancelled &&
                        !h.IsDeleted)
            .OrderByDescending(h => h.DueDate)
            .ToListAsync(cancellationToken);

        var assignmentIds = assignments.Select(h => h.Id).ToList();
        var mySubmissions = await _context.AssignmentSubmissions
            .Where(s => s.StudentId == student.Id && assignmentIds.Contains(s.HomeworkAssignmentId) && !s.IsDeleted)
            .ToDictionaryAsync(s => s.HomeworkAssignmentId, cancellationToken);

        var list = assignments.Select(h => {
            mySubmissions.TryGetValue(h.Id, out var sub);
            return new StudentAssignmentItemDto(
                h.Id,
                h.Title,
                h.Description,
                h.SubjectCourse != null ? h.SubjectCourse.Name : "Subject",
                h.TeacherUser != null ? h.TeacherUser.FullName : "Teacher",
                h.DueDate,
                h.MaxMarks,
                h.AttachmentUrl,
                h.AttachmentFileName,
                h.Status.ToString(),
                sub != null,
                sub?.Status,
                sub?.MarksAwarded,
                sub?.Feedback
            );
        }).ToList();

        return ApiSuccess(list);
    }

    [HttpPost("assignments/{id:guid}/submit")]
    public async Task<IActionResult> SubmitAssignment(Guid id, [FromBody] SubmitAssignmentRequest request, CancellationToken cancellationToken)
    {
        var student = await ResolveCurrentStudentAsync(cancellationToken);
        if (student == null) return ApiNotFound("Student record not found.");

        var assignment = await _context.HomeworkAssignments.FirstOrDefaultAsync(h => h.Id == id && h.OrganizationId == student.OrganizationId && !h.IsDeleted, cancellationToken);
        if (assignment == null) return ApiNotFound("Assignment not found.");

        if (assignment.Status != AssignmentStatus.Active)
            return ApiError("This assignment is closed and cannot accept submissions.");

        var existing = await _context.AssignmentSubmissions.FirstOrDefaultAsync(s => s.HomeworkAssignmentId == id && s.StudentId == student.Id && !s.IsDeleted, cancellationToken);
        if (existing != null)
        {
            if (existing.Status == SubmissionStatus.Graded)
                return ApiError("Assignment has already been graded and cannot be resubmitted.");

            existing.Content = request.EffectiveContent;
            existing.AttachmentUrl = request.AttachmentUrl;
            existing.AttachmentFileName = request.AttachmentFileName;
            existing.SubmissionDate = DateTime.UtcNow;
            existing.Status = DateTime.UtcNow > assignment.DueDate ? SubmissionStatus.Late : SubmissionStatus.Submitted;
            existing.UpdatedAtUtc = DateTime.UtcNow;
        }
        else
        {
            var submission = new AssignmentSubmission
            {
                Id = Guid.NewGuid(),
                OrganizationId = student.OrganizationId,
                BranchId = student.BranchId,
                HomeworkAssignmentId = id,
                StudentId = student.Id,
                SubmissionDate = DateTime.UtcNow,
                Content = request.EffectiveContent,
                AttachmentUrl = request.AttachmentUrl,
                AttachmentFileName = request.AttachmentFileName,
                Status = DateTime.UtcNow > assignment.DueDate ? SubmissionStatus.Late : SubmissionStatus.Submitted
            };
            _context.AssignmentSubmissions.Add(submission);
        }

        await _context.SaveChangesAsync(cancellationToken);
        return ApiSuccess("Assignment submitted successfully.");
    }

    [HttpGet("assignments/{id:guid}/my-submission")]
    [HttpGet("assignments/{id:guid}/submission")]
    public async Task<IActionResult> GetMySubmission(Guid id, CancellationToken cancellationToken)
    {
        var student = await ResolveCurrentStudentAsync(cancellationToken);
        if (student == null) return ApiNotFound("Student record not found.");

        var submission = await _context.AssignmentSubmissions
            .Include(s => s.HomeworkAssignment)
            .FirstOrDefaultAsync(s => s.HomeworkAssignmentId == id && s.StudentId == student.Id && !s.IsDeleted, cancellationToken);

        if (submission == null) return ApiSuccess((object?)null, "No submission found for this assignment.");

        return ApiSuccess(new {
            submission.Id,
            submission.SubmissionDate,
            submission.Content,
            SubmissionText = submission.Content,
            submission.AttachmentUrl,
            submission.AttachmentFileName,
            Status = submission.Status.ToString(),
            SubmissionStatus = submission.Status.ToString(),
            submission.MarksAwarded,
            MaxMarks = submission.HomeworkAssignment?.MaxMarks,
            submission.Feedback,
            TeacherFeedback = submission.Feedback,
            submission.GradedAtUtc
        });
    }

    // =========================================================================
    // 7. STUDENT EXAMS & RESULTS (FINALIZED ONLY)
    // =========================================================================

    [HttpGet("exams")]
    public async Task<IActionResult> GetExams([FromQuery] Guid? studentId, CancellationToken cancellationToken)
    {
        var student = await ResolveCurrentStudentAsync(cancellationToken, studentId);
        if (student == null) return ApiNotFound("Student record not found.");

        var classGrade = await _context.ClassGrades.FirstOrDefaultAsync(c => c.OrganizationId == student.OrganizationId && c.Name == student.CurrentGrade && !c.IsDeleted, cancellationToken);
        if (classGrade == null) return ApiSuccess(new List<StudentExamItemDto>());

        var exams = await _context.ExamSchedules
            .Include(e => e.SubjectCourse)
            .Where(e => e.OrganizationId == student.OrganizationId &&
                        e.ClassGradeId == classGrade.Id &&
                        !e.IsDeleted)
            .OrderByDescending(e => e.ExamDate)
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

        return ApiSuccess(exams);
    }

    [HttpGet("results")]
    public async Task<IActionResult> GetResults([FromQuery] Guid? studentId, CancellationToken cancellationToken)
    {
        var student = await ResolveCurrentStudentAsync(cancellationToken, studentId);
        if (student == null) return ApiNotFound("Student record not found.");

        // SECURITY: ONLY published and finalized results are visible to students
        var results = await _context.ExamResults
            .Include(r => r.ExamSchedule).ThenInclude(es => es!.SubjectCourse)
            .Where(r => r.OrganizationId == student.OrganizationId &&
                        r.StudentId == student.Id &&
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

        return ApiSuccess(results);
    }

    // =========================================================================
    // 8. STUDENT FEES (STRICTLY OWN INVOICES)
    // =========================================================================

    [HttpGet("fees")]
    public async Task<IActionResult> GetFees([FromQuery] Guid? studentId, CancellationToken cancellationToken)
    {
        var student = await ResolveCurrentStudentAsync(cancellationToken, studentId);
        if (student == null) return ApiNotFound("Student record not found.");

        var invoices = await _context.FeeInvoices
            .Include(f => f.Payments)
            .Where(f => f.OrganizationId == student.OrganizationId && f.StudentId == student.Id && !f.IsDeleted)
            .OrderByDescending(f => f.IssueDate)
            .Select(f => new StudentFeeInvoiceDto(
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
                    p.PaymentMethod,
                    p.TransactionReference
                )).ToList()
            ))
            .ToListAsync(cancellationToken);

        return ApiSuccess(invoices);
    }

    // =========================================================================
    // 9. STUDENT DOCUMENTS (ID CARD, RECEIPTS, CERTIFICATES)
    // =========================================================================

    [HttpGet("documents")]
    public async Task<IActionResult> GetDocuments([FromQuery] Guid? studentId, CancellationToken cancellationToken)
    {
        var student = await ResolveCurrentStudentAsync(cancellationToken, studentId);
        if (student == null) return ApiNotFound("Student record not found.");

        var docs = await _context.StudentDocuments
            .Where(d => d.OrganizationId == student.OrganizationId && d.StudentId == student.Id && !d.IsDeleted)
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

        return ApiSuccess(docs);
    }

    [HttpGet("documents/{id:guid}/download")]
    [HttpGet("documents/{id:guid}")]
    public async Task<IActionResult> DownloadDocument(Guid id, CancellationToken cancellationToken)
    {
        var student = await ResolveCurrentStudentAsync(cancellationToken);
        if (student == null) return ApiNotFound("Student record not found.");

        var doc = await _context.StudentDocuments.FirstOrDefaultAsync(d => d.Id == id && d.OrganizationId == student.OrganizationId && !d.IsDeleted, cancellationToken);
        if (doc == null) return ApiNotFound("Document not found.");

        // SECURITY: Verify document belongs to the requesting student
        if (doc.StudentId != student.Id && !CurrentUserService.IsSuperAdmin && CurrentUserService.RoleType != UserRoleType.OrganizationAdmin)
            return ApiForbidden("You are not authorized to access this document.");

        return ApiSuccess(new {
            doc.Id,
            doc.Title,
            doc.FileName,
            doc.FileUrl,
            doc.DocumentType,
            doc.FileSizeBytes
        });
    }

    // =========================================================================
    // 10. STUDENT LEAVE REQUESTS
    // =========================================================================

    [HttpGet("leaves")]
    [HttpGet("leave")]
    public async Task<IActionResult> GetLeaves([FromQuery] Guid? studentId, CancellationToken cancellationToken)
    {
        var student = await ResolveCurrentStudentAsync(cancellationToken, studentId);
        if (student == null) return ApiNotFound("Student record not found.");

        var leaves = await _context.LeaveRequests
            .Where(l => l.OrganizationId == student.OrganizationId && l.StudentId == student.Id && !l.IsDeleted)
            .OrderByDescending(l => l.CreatedAtUtc)
            .Select(l => new LeaveRequestDto(
                l.Id,
                student.FullName,
                l.RequesterType.ToString(),
                l.LeaveType,
                l.StartDate,
                l.EndDate,
                l.Reason,
                l.Status,
                l.Status.ToString(),
                l.DecisionRemarks,
                l.DecisionDateUtc,
                l.CreatedAtUtc
            ))
            .ToListAsync(cancellationToken);

        return ApiSuccess(leaves);
    }

    [HttpPost("leaves")]
    [HttpPost("leave")]
    public async Task<IActionResult> ApplyLeave([FromBody] ApplyLeaveRequest request, CancellationToken cancellationToken)
    {
        var student = await ResolveCurrentStudentAsync(cancellationToken);
        if (student == null) return ApiNotFound("Student record not found.");

        if (string.IsNullOrWhiteSpace(request.Reason))
            return ApiError("Leave reason is required.");

        if (request.EndDate < request.StartDate)
            return ApiError("End date cannot be earlier than start date.");

        var leave = new LeaveRequest
        {
            Id = Guid.NewGuid(),
            OrganizationId = student.OrganizationId,
            BranchId = student.BranchId,
            RequesterUserId = student.UserId ?? Guid.NewGuid(),
            RequesterType = RequesterType.Student,
            StudentId = student.Id,
            LeaveType = request.LeaveType ?? "Sick",
            StartDate = request.StartDate.Date,
            EndDate = request.EndDate.Date,
            Reason = request.Reason.Trim(),
            AttachmentUrl = request.AttachmentUrl,
            Status = LeaveStatus.Pending
        };

        _context.LeaveRequests.Add(leave);
        await _context.SaveChangesAsync(cancellationToken);

        return ApiSuccess(new { id = leave.Id }, "Student leave application submitted successfully.");
    }

    [HttpPost("leaves/{id:guid}/cancel")]
    [HttpPost("leave/{id:guid}/cancel")]
    public async Task<IActionResult> CancelLeave(Guid id, CancellationToken cancellationToken)
    {
        var student = await ResolveCurrentStudentAsync(cancellationToken);
        if (student == null) return ApiNotFound("Student record not found.");

        var leave = await _context.LeaveRequests.FirstOrDefaultAsync(l => l.Id == id && l.OrganizationId == student.OrganizationId && l.StudentId == student.Id && !l.IsDeleted, cancellationToken);
        if (leave == null) return ApiNotFound("Leave request not found.");

        if (leave.Status != LeaveStatus.Pending)
            return ApiError("Only pending leave requests can be cancelled.");

        leave.Status = LeaveStatus.Cancelled;
        await _context.SaveChangesAsync(cancellationToken);

        return ApiSuccess("Leave request cancelled successfully.");
    }

    // =========================================================================
    // 11. STUDENT ANNOUNCEMENTS
    // =========================================================================

    [HttpGet("announcements")]
    public async Task<IActionResult> GetAnnouncements(CancellationToken cancellationToken)
    {
        Guid orgId = GetTargetOrgId();
        if (orgId == Guid.Empty) return ApiError("Tenant context required.", StatusCodes.Status400BadRequest);

        var announcements = await _context.Announcements
            .Where(a => a.OrganizationId == orgId &&
                        !a.IsDeleted &&
                        (a.TargetAudience == "All" || a.TargetAudience == "Students"))
            .OrderByDescending(a => a.PublishDate)
            .Select(a => new StudentAnnouncementDto(a.Id, a.Title, a.Content, a.Priority, a.PublishDate))
            .ToListAsync(cancellationToken);

        return ApiSuccess(announcements);
    }
}
