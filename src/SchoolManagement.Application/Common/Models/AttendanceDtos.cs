using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Application.Common.Models;

public record DetailedAttendanceRecordDto(
    Guid Id,
    Guid StudentId,
    string AdmissionNumber,
    string? RollNumber,
    string StudentName,
    Guid ClassGradeId,
    string ClassGradeName,
    Guid? ClassSectionId,
    string? ClassSectionName,
    DateTime Date,
    AttendanceStatus Status,
    string StatusName,
    string? Remarks,
    bool IsLocked,
    string? MarkedByStaffName
);

public record AttendanceSummaryDto(
    DateTime Date,
    int TotalStudents,
    int PresentCount,
    int AbsentCount,
    int LateCount,
    int ExcusedCount,
    double AttendancePercentage,
    IReadOnlyList<DetailedAttendanceRecordDto> Records
);

public record MonthlyAttendanceSummaryDto(
    int Year,
    int Month,
    int TotalWorkingDays,
    double OverallPercentage,
    IReadOnlyList<StudentAttendanceRateDto> Students
);

public record StudentAttendanceRateDto(
    Guid StudentId,
    string AdmissionNumber,
    string? RollNumber,
    string FullName,
    string Grade,
    string? Section,
    int TotalDays,
    int PresentDays,
    int AbsentDays,
    int LateDays,
    int ExcusedDays,
    double Percentage,
    bool IsLowAttendance
);

public record LockAttendanceRequestDto(
    DateTime Date,
    Guid ClassGradeId,
    Guid? ClassSectionId = null
);

public record UnlockAttendanceRequestDto(
    DateTime Date,
    Guid ClassGradeId,
    Guid? ClassSectionId = null
);

public record CorrectAttendanceRequestDto(
    Guid RecordId,
    AttendanceStatus NewStatus,
    string Reason
);

public record AdminMarkAttendanceRequestDto(
    Guid ClassGradeId,
    Guid? ClassSectionId,
    Guid? SubjectCourseId,
    DateTime Date,
    List<AdminAttendanceEntryDto> Entries,
    bool LockImmediately = false
);

public record AdminAttendanceEntryDto(
    Guid StudentId,
    AttendanceStatus Status,
    string? Remarks = null
);
