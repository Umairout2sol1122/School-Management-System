namespace SchoolManagement.Application.Common.Models;

public record StudentReportRowDto(
    Guid Id,
    string AdmissionNumber,
    string? RollNumber,
    string FullName,
    string CurrentGrade,
    string? Section,
    string? BranchName,
    string Gender,
    string Status,
    DateTime AdmissionDate,
    string? PrimaryGuardianName,
    string? PrimaryGuardianPhone
);

public record StaffReportRowDto(
    Guid Id,
    string EmployeeCode,
    string FullName,
    string Email,
    string Phone,
    string Designation,
    string Department,
    string? BranchName,
    string EmploymentType,
    string Status,
    DateTime JoiningDate
);

public record BranchReportRowDto(
    Guid Id,
    string Name,
    string Code,
    string? Address,
    string? Phone,
    bool IsMainBranch,
    bool IsActive,
    int TotalStudents,
    int TotalStaff
);

public record ClassUtilizationReportRowDto(
    Guid Id,
    string Name,
    string Code,
    string? DepartmentName,
    int Capacity,
    int EnrolledStudents,
    int SectionsCount,
    double UtilizationPercentage
);

public record BulkImportResultDto(
    int TotalRows,
    int SuccessCount,
    int FailureCount,
    IReadOnlyList<string> Errors
);

public record AttendanceReportRowDto(
    string ClassGrade,
    string? Section,
    string? BranchName,
    int TotalRecords,
    int PresentCount,
    int AbsentCount,
    int LateCount,
    int LeaveCount,
    double AttendancePercentage
);

public record FeeCollectionReportRowDto(
    string ClassGrade,
    string? Section,
    string? BranchName,
    int TotalInvoices,
    decimal TotalBilled,
    decimal TotalPaid,
    decimal TotalBalance,
    double CollectionPercentage,
    int DefaultersCount
);

public record ExamPerformanceReportRowDto(
    Guid ExamId,
    string ExamTitle,
    string SubjectName,
    string ClassGrade,
    int TotalCandidates,
    int PassedCandidates,
    int FailedCandidates,
    double PassPercentage,
    decimal AverageScore,
    decimal HighestScore,
    decimal LowestScore
);


public record ImportStudentsRequest(
    string? CsvContent = null,
    Guid? BranchId = null
);
