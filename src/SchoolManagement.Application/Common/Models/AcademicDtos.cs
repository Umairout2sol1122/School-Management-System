using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Application.Common.Models;

// Academic Session DTOs
public record AcademicSessionDto(
    Guid Id,
    Guid OrganizationId,
    Guid? BranchId,
    string? BranchName,
    string Name,
    string Code,
    string Type,
    DateTime StartDate,
    DateTime EndDate,
    bool IsCurrent,
    bool IsActive
);

public record CreateAcademicSessionRequest(
    Guid? BranchId,
    string Name,
    string Code,
    DateTime StartDate,
    DateTime EndDate,
    string Type = "Annual",
    bool IsCurrent = false
);

public record UpdateAcademicSessionRequest(
    Guid? BranchId,
    string Name,
    string Code,
    string Type,
    DateTime StartDate,
    DateTime EndDate,
    bool IsCurrent,
    bool IsActive
);

// Department DTOs
public record DepartmentDto(
    Guid Id,
    Guid OrganizationId,
    Guid? BranchId,
    string Name,
    string Code,
    string? Description,
    Guid? HeadOfDepartmentUserId,
    string? HeadOfDepartmentName,
    bool IsActive,
    int TotalStaff,
    int TotalCourses
);

public record CreateDepartmentRequest(
    Guid? BranchId,
    string Name,
    string Code,
    string? Description,
    Guid? HeadOfDepartmentUserId
);

public record UpdateDepartmentRequest(
    Guid? BranchId,
    string Name,
    string Code,
    string? Description,
    Guid? HeadOfDepartmentUserId,
    bool IsActive
);

// Class / Grade DTOs
public record ClassGradeDto(
    Guid Id,
    Guid OrganizationId,
    Guid? BranchId,
    Guid? DepartmentId,
    string? DepartmentName,
    string Name,
    string Code,
    int DisplayOrder,
    int Capacity,
    bool IsActive,
    int SectionsCount,
    int StudentsCount
);

public record CreateClassGradeRequest(
    Guid? BranchId,
    Guid? DepartmentId,
    string Name,
    string Code,
    int DisplayOrder = 1,
    int Capacity = 40
);

public record UpdateClassGradeRequest(
    Guid? BranchId,
    Guid? DepartmentId,
    string Name,
    string Code,
    int DisplayOrder,
    int Capacity,
    bool IsActive
);

// Section DTOs
public record ClassSectionDto(
    Guid Id,
    Guid ClassGradeId,
    string ClassGradeName,
    Guid? BranchId,
    string Name,
    string? RoomNumber,
    int Capacity,
    Guid? ClassTeacherUserId,
    string? ClassTeacherName,
    bool IsActive,
    int StudentsCount
);

public record CreateClassSectionRequest(
    Guid? ClassGradeId = null,
    Guid? BranchId = null,
    string Name = "",
    string? RoomNumber = null,
    int Capacity = 40,
    Guid? ClassTeacherUserId = null,
    Guid? ClassId = null
)
{
    public Guid? EffectiveClassGradeId => ClassGradeId.HasValue && ClassGradeId.Value != Guid.Empty ? ClassGradeId : ClassId;
}

public record UpdateClassSectionRequest(
    string Name,
    string? RoomNumber,
    int Capacity,
    Guid? ClassTeacherUserId,
    bool IsActive
);

// Subject / Course DTOs
public record SubjectCourseDto(
    Guid Id,
    Guid OrganizationId,
    Guid? BranchId,
    Guid? DepartmentId,
    string? DepartmentName,
    Guid? ClassGradeId,
    string? ClassGradeName,
    string Name,
    string Code,
    int CreditHours,
    string Type,
    bool IsActive
);

public record CreateSubjectCourseRequest(
    Guid? BranchId,
    Guid? DepartmentId,
    Guid? ClassGradeId,
    string Name,
    string Code,
    int CreditHours = 3,
    string Type = "Core",
    Guid? ClassId = null
)
{
    public Guid? EffectiveClassGradeId => ClassGradeId.HasValue && ClassGradeId.Value != Guid.Empty ? ClassGradeId : ClassId;
}

public record UpdateSubjectCourseRequest(
    Guid? BranchId,
    Guid? DepartmentId,
    Guid? ClassGradeId,
    string Name,
    string Code,
    int CreditHours,
    string Type,
    bool IsActive
);

// Course Assignment DTOs
public record CourseAssignmentDto(
    Guid Id,
    Guid OrganizationId,
    Guid ClassSectionId,
    string ClassSectionName,
    string ClassGradeName,
    Guid SubjectCourseId,
    string SubjectCourseName,
    string SubjectCourseCode,
    Guid TeacherUserId,
    string TeacherName,
    Guid? AcademicSessionId,
    string? AcademicSessionName,
    bool IsActive
);

public record CreateCourseAssignmentRequest(
    Guid ClassSectionId,
    Guid SubjectCourseId,
    Guid TeacherUserId,
    Guid? AcademicSessionId
);

public record LeaveDecisionRequest(
    LeaveStatus Status,
    string? DecisionRemarks
);

public record CreateFeeInvoiceRequest(
    Guid StudentId,
    Guid? BranchId,
    string Title,
    DateTime DueDate,
    decimal TotalAmount
);

public record RecordFeePaymentRequest(
    decimal AmountPaid,
    string PaymentMethod = "BankTransfer",
    string? TransactionReference = null
);

public record PromoteStudentsRequest(
    string FromGrade,
    string ToGrade,
    string? FromSection = null,
    string? ToSection = null,
    List<Guid>? StudentIds = null,
    Guid? AcademicSessionId = null
);

public record RetainStudentsRequest(
    string Grade,
    string? Section = null,
    List<Guid>? StudentIds = null,
    string? Reason = null
);

public record PromotionResultDto(
    int ProcessedCount,
    string FromGrade,
    string ToGrade,
    string Action,
    IReadOnlyList<string> StudentNames
);

public record StudentReportCardDto(
    Guid StudentId,
    string AdmissionNumber,
    string? RollNumber,
    string StudentName,
    string Grade,
    string? Section,
    string Term,
    DateTime GeneratedDate,
    decimal TotalPossibleMarks,
    decimal TotalMarksObtained,
    decimal OverallPercentage,
    string OverallGrade,
    decimal OverallGpa,
    string OverallStatus,
    IReadOnlyList<ReportCardSubjectItemDto> Subjects
);

public record ReportCardSubjectItemDto(
    string SubjectName,
    string SubjectCode,
    decimal TotalMarks,
    decimal PassingMarks,
    decimal MarksObtained,
    decimal Percentage,
    string? Grade,
    decimal? Gpa,
    bool IsPassed,
    string? Remarks
);

public record ExamReviewSummaryDto(
    Guid ExamId,
    string Title,
    string Term,
    string SubjectName,
    string ClassGradeName,
    decimal TotalMarks,
    int TotalStudents,
    int MarksEnteredCount,
    int PassedCount,
    int FailedCount,
    decimal HighestMarks,
    decimal LowestMarks,
    decimal AverageMarks,
    bool IsPublished,
    bool IsLocked
);

public record UpdateTimetableSlotRequest(
    TimeSpan StartTime,
    TimeSpan EndTime,
    string? RoomNumber = null,
    Guid? TeacherStaffId = null
);

public record AssignSubstituteTeacherRequest(
    Guid SubstituteTeacherStaffId,
    string? SubstitutionNotes = null
);



