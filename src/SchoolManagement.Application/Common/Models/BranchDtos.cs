namespace SchoolManagement.Application.Common.Models;

public record CreateBranchRequestDto(
    Guid OrganizationId,
    string Name,
    string Code,
    string? Address = null,
    string? Phone = null,
    string? Email = null,
    bool IsMainBranch = false
);

public record UpdateBranchRequestDto(
    string Name,
    string? Address = null,
    string? Phone = null,
    string? Email = null,
    bool IsMainBranch = false
);

public record BranchDto(
    Guid Id,
    Guid OrganizationId,
    string? OrganizationName,
    string Name,
    string Code,
    string Address,
    string Phone,
    string Email,
    bool IsMainBranch,
    bool IsActive,
    DateTime CreatedAtUtc
);

public record BranchDashboardDto(
    Guid BranchId,
    Guid OrganizationId,
    string BranchName,
    string BranchCode,
    bool IsActive,
    bool IsMainBranch,
    string? Address,
    string? Phone,
    string? Email,
    int TotalStudents,
    int TotalTeachers,
    int TotalStaff,
    int TotalClasses,
    int TotalSections,
    double TodayAttendancePercentage,
    decimal TotalFeesCollectedThisMonth,
    decimal TotalFeesPending
);

public record BranchUserDto(
    Guid UserId,
    string FullName,
    string Email,
    string? PhoneNumber,
    string Role,
    bool IsActive,
    DateTime CreatedAtUtc
);

public record UpdateBranchSettingsRequestDto(
    string? OperatingHours = null,
    string? ContactPhone = null,
    string? ContactEmail = null,
    int? DefaultClassCapacity = null,
    string? Notes = null
);
