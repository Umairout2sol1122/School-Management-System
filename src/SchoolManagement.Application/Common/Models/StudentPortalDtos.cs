using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Application.Common.Models;

public record StudentProfileDto(
    Guid StudentId,
    Guid? UserId,
    string AdmissionNumber,
    string? RollNumber,
    string FirstName,
    string LastName,
    string FullName,
    DateTime DateOfBirth,
    string Gender,
    string? BloodGroup,
    string CurrentGrade,
    string? Section,
    DateTime AdmissionDate,
    string Status,
    string? EmergencyContactName,
    string? EmergencyContactPhone,
    string? Address,
    string? City,
    string? GuardianName,
    string? GuardianPhone,
    string? GuardianRelationship
);

public record UpdateStudentContactRequest(
    string? EmergencyContactName,
    string? EmergencyContactPhone,
    string? Address,
    string? City
);

public record StudentDashboardDto(
    string StudentName,
    string AdmissionNumber,
    string? RollNumber,
    string Grade,
    string? Section,
    string BranchName,
    double OverallAttendancePercentage,
    int PresentDays,
    int AbsentDays,
    int TotalDays,
    int PendingAssignmentsCount,
    int UpcomingExamsCount,
    decimal OutstandingFeesBalance,
    IReadOnlyList<StudentTimetableItemDto> TodaySchedule,
    IReadOnlyList<StudentAnnouncementDto> RecentAnnouncements
)
{
    public string CurrentGrade => Grade;
}

public record StudentAttendanceDto(
    DateTime Date,
    AttendanceStatus Status,
    string StatusName,
    string? SubjectName,
    string? Remarks
);

public record StudentTimetableItemDto(
    DayOfWeek DayOfWeek,
    string DayName,
    TimeSpan StartTime,
    TimeSpan EndTime,
    string SubjectName,
    string? TeacherName,
    string? RoomNumber
);

public record StudentAssignmentItemDto(
    Guid Id,
    string Title,
    string Description,
    string SubjectName,
    string TeacherName,
    DateTime DueDate,
    int MaxMarks,
    string? AttachmentUrl,
    string? AttachmentFileName,
    string Status,
    bool HasSubmitted,
    SubmissionStatus? MySubmissionStatus,
    decimal? MyMarksAwarded,
    string? MyFeedback
);

public record SubmitAssignmentRequest(
    string? Content = null,
    string? AttachmentUrl = null,
    string? AttachmentFileName = null,
    string? SubmissionText = null
)
{
    public string? EffectiveContent => !string.IsNullOrWhiteSpace(Content) ? Content : SubmissionText;
}

public record StudentExamItemDto(
    Guid Id,
    string Title,
    string Term,
    string SubjectName,
    DateTime ExamDate,
    TimeSpan StartTime,
    TimeSpan EndTime,
    string? RoomNumber,
    decimal TotalMarks,
    decimal PassingMarks
);

public record StudentResultItemDto(
    string ExamTitle,
    string Term,
    string SubjectName,
    DateTime ExamDate,
    decimal MarksObtained,
    decimal TotalMarks,
    decimal Percentage,
    string? Grade,
    decimal? Gpa,
    string? Remarks
);

public record StudentFeeInvoiceDto(
    Guid Id,
    string InvoiceNumber,
    string Title,
    DateTime IssueDate,
    DateTime DueDate,
    decimal TotalAmount,
    decimal PaidAmount,
    decimal Balance,
    FeeStatus Status,
    string StatusName,
    IReadOnlyList<FeePaymentDto> Payments
);

public record FeePaymentDto(
    Guid Id,
    string ReceiptNumber,
    decimal AmountPaid,
    DateTime PaymentDate,
    string PaymentMethod,
    string? TransactionReference
);

public record StudentDocumentDto(
    Guid Id,
    string Title,
    StudentDocumentType DocumentType,
    string DocumentTypeName,
    string FileName,
    string FileUrl,
    long FileSizeBytes,
    string? Description,
    DateTime UploadedAtUtc
);

public record StudentAnnouncementDto(
    Guid Id,
    string Title,
    string Content,
    string Priority,
    DateTime PublishedAtUtc
);

public record UploadStudentDocumentRequest(
    string Title,
    StudentDocumentType DocumentType,
    string? FileUrl = null,
    string? FileName = null,
    long FileSizeBytes = 1024,
    string? Description = null
);

public record CreateStudentPortalAccessRequest(
    string? Email = null,
    string? Username = null,
    string? Password = null
);

