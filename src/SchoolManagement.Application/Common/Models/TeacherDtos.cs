using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Application.Common.Models;

public record TeacherProfileDto(
    Guid Id,
    Guid UserId,
    string EmployeeCode,
    string FullName,
    string Email,
    string? PhoneNumber,
    string Designation,
    string Department,
    string? Qualification,
    string? Specialization,
    DateTime JoiningDate,
    string EmploymentType,
    string Status,
    string? EmergencyContactName,
    string? EmergencyContactPhone,
    string? Address,
    int AssignedClassesCount,
    int AssignedSubjectsCount
);

public record UpdateTeacherProfileRequest(
    string? PhoneNumber,
    string? Address,
    string? Qualification,
    string? Specialization,
    string? EmergencyContactName,
    string? EmergencyContactPhone
);

public record TeacherDashboardClassSummaryDto(
    Guid ClassGradeId,
    string GradeName,
    string SectionName
);

public record TeacherDashboardDto(
    string TeacherName,
    string EmployeeCode,
    string Department,
    string Designation,
    string BranchName,
    int TotalAssignedClasses,
    int TotalAssignedSubjects,
    int TotalStudentsUnderTeacher,
    int PendingAttendanceCount,
    int UpcomingExamsCount,
    int ActiveAssignmentsCount,
    int PendingSubmissionsToGradeCount,
    IReadOnlyList<TeacherTimetableItemDto> TodaySchedule,
    IReadOnlyList<TeacherAnnouncementDto> RecentAnnouncements,
    IReadOnlyList<TeacherDashboardClassSummaryDto>? AssignedClasses = null
);

public record TeacherClassItemDto(
    Guid ClassGradeId,
    string ClassGradeName,
    string ClassGradeCode,
    Guid ClassSectionId,
    string SectionName,
    string? RoomNumber,
    Guid SubjectCourseId,
    string SubjectName,
    string SubjectCode,
    int StudentCount
);

public record TeacherSubjectItemDto(
    Guid SubjectCourseId,
    string Name,
    string Code,
    int CreditHours,
    string? DepartmentName,
    IReadOnlyList<string> AssignedClasses
);

public record TeacherStudentItemDto(
    Guid StudentId,
    string AdmissionNumber,
    string? RollNumber,
    string FullName,
    string Grade,
    string? Section,
    string Gender,
    string? EmergencyContactPhone,
    double AttendancePercentage
);

public record StudentAttendanceEntryDto(
    Guid StudentId,
    AttendanceStatus Status,
    string? Remarks
);

public record MarkAttendanceRequest(
    Guid ClassGradeId,
    Guid? ClassSectionId,
    Guid? SubjectCourseId,
    DateTime Date,
    IReadOnlyList<StudentAttendanceEntryDto>? Students = null,
    IReadOnlyList<StudentAttendanceEntryDto>? Entries = null,
    bool? IsEdit = false
)
{
    public IReadOnlyList<StudentAttendanceEntryDto> EffectiveStudents =>
        Students ?? Entries ?? Array.Empty<StudentAttendanceEntryDto>();
}

public record AttendanceRecordDto(
    Guid Id,
    Guid StudentId,
    string StudentAdmissionNumber,
    string StudentName,
    DateTime Date,
    AttendanceStatus Status,
    string StatusName,
    string? Remarks,
    bool IsLocked
);

public record TeacherTimetableItemDto(
    Guid Id,
    DayOfWeek DayOfWeek,
    string DayName,
    TimeSpan StartTime,
    TimeSpan EndTime,
    string ClassGradeName,
    string? SectionName,
    string SubjectName,
    string? RoomNumber
);

public record CreateTimetableSlotRequest(
    Guid? AcademicSessionId,
    Guid ClassGradeId,
    Guid? ClassSectionId,
    Guid SubjectCourseId,
    Guid? TeacherStaffId,
    DayOfWeek DayOfWeek,
    TimeSpan StartTime,
    TimeSpan EndTime,
    string? RoomNumber
);

public record CreateHomeworkRequest(
    Guid ClassGradeId,
    Guid? ClassSectionId,
    Guid SubjectCourseId,
    string Title,
    string Description,
    DateTime? DueDate = null,
    DateTime? DueDateUtc = null,
    int? MaxMarks = null,
    decimal? TotalMarks = null,
    string? AttachmentUrl = null,
    string? AttachmentFileName = null
)
{
    public DateTime EffectiveDueDate => DueDateUtc ?? DueDate ?? DateTime.UtcNow.AddDays(7);
    public int EffectiveMaxMarks => MaxMarks ?? (TotalMarks.HasValue ? (int)TotalMarks.Value : 100);
}

public record HomeworkAssignmentDto(
    Guid Id,
    Guid ClassGradeId,
    string ClassGradeName,
    Guid? ClassSectionId,
    string? SectionName,
    Guid SubjectCourseId,
    string SubjectName,
    string Title,
    string Description,
    DateTime DueDate,
    int MaxMarks,
    string? AttachmentUrl,
    string? AttachmentFileName,
    AssignmentStatus Status,
    int TotalSubmissionsCount,
    int GradedSubmissionsCount
);

public record AssignmentSubmissionDto(
    Guid Id,
    Guid HomeworkAssignmentId,
    Guid StudentId,
    string StudentAdmissionNumber,
    string StudentName,
    DateTime SubmissionDate,
    string? Content,
    string? AttachmentUrl,
    string? AttachmentFileName,
    SubmissionStatus Status,
    string StatusName,
    decimal? MarksAwarded,
    string? Feedback,
    DateTime? GradedAtUtc
);

public record GradeSubmissionRequest(
    Guid? StudentId = null,
    decimal MarksAwarded = 0,
    string? Feedback = null,
    string? TeacherFeedback = null
)
{
    public string? EffectiveFeedback => TeacherFeedback ?? Feedback;
}

public record CreateExamScheduleRequest(
    Guid? AcademicSessionId,
    Guid ClassGradeId,
    Guid? ClassSectionId,
    Guid SubjectCourseId,
    string Title,
    string? Term = null,
    DateTime? ExamDate = null,
    DateTime? ExamDateUtc = null,
    TimeSpan? StartTime = null,
    TimeSpan? EndTime = null,
    string? RoomNumber = null,
    decimal TotalMarks = 100,
    decimal PassingMarks = 40
)
{
    public string EffectiveTerm => string.IsNullOrWhiteSpace(Term) ? "Mid-Term" : Term;
    public DateTime EffectiveExamDate => ExamDateUtc ?? ExamDate ?? DateTime.UtcNow.AddDays(7);
    public TimeSpan EffectiveStartTime => StartTime ?? TimeSpan.FromHours(9);
    public TimeSpan EffectiveEndTime => EndTime ?? TimeSpan.FromHours(10.5);
}

public record ExamScheduleDto(
    Guid Id,
    string Title,
    string Term,
    Guid ClassGradeId,
    string ClassGradeName,
    Guid? ClassSectionId,
    string? SectionName,
    Guid SubjectCourseId,
    string SubjectName,
    DateTime ExamDate,
    TimeSpan StartTime,
    TimeSpan EndTime,
    string? RoomNumber,
    decimal TotalMarks,
    decimal PassingMarks,
    bool IsPublished,
    bool IsLocked,
    int ResultsEnteredCount
);

public record StudentExamMarkEntryDto(
    Guid StudentId,
    decimal MarksObtained,
    string? Remarks
);

public record EnterExamMarksRequest(
    IReadOnlyList<StudentExamMarkEntryDto>? Marks = null,
    IReadOnlyList<StudentExamMarkEntryDto>? Entries = null
)
{
    public IReadOnlyList<StudentExamMarkEntryDto> EffectiveMarks =>
        Marks ?? Entries ?? Array.Empty<StudentExamMarkEntryDto>();
}

public record ExamResultDto(
    Guid Id,
    Guid ExamScheduleId,
    Guid StudentId,
    string StudentAdmissionNumber,
    string StudentName,
    decimal MarksObtained,
    decimal TotalMarks,
    string? Grade,
    decimal Percentage,
    decimal? Gpa,
    string? Remarks,
    bool IsFinalized
);

public record ApplyLeaveRequest(
    string LeaveType,
    DateTime StartDate,
    DateTime EndDate,
    string Reason,
    string? AttachmentUrl
);

public record LeaveRequestDto(
    Guid Id,
    string RequesterName,
    string RequesterType,
    string LeaveType,
    DateTime StartDate,
    DateTime EndDate,
    string Reason,
    LeaveStatus Status,
    string StatusName,
    string? DecisionRemarks,
    DateTime? DecisionDateUtc,
    DateTime CreatedAtUtc
);

public record TeacherAnnouncementDto(
    Guid Id,
    string Title,
    string Content,
    string Priority,
    DateTime PublishedAtUtc
);

public record TeacherAttendanceReportDto(
    string ClassGradeName,
    string? SectionName,
    int TotalStudents,
    double PresentAveragePercentage,
    int DaysRecorded,
    IReadOnlyList<TeacherStudentItemDto> Students
)
{
    public IReadOnlyList<TeacherStudentItemDto> Records => Students;
}
