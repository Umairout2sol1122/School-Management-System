using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Application.Common.Models;

public record CreateGuardianRequestDto(
    Guid? BranchId,
    string FirstName,
    string? LastName,
    GuardianRelationshipType RelationshipType,
    string? NationalId,
    string? Email,
    string Phone,
    string? AlternatePhone,
    string? Occupation,
    decimal? AnnualIncome,
    string? Address,
    string? City,
    bool IsEmergencyContact
);

public record UpdateGuardianRequestDto(
    string FirstName,
    string LastName,
    GuardianRelationshipType RelationshipType,
    string? NationalId,
    string? Email,
    string Phone,
    string? AlternatePhone,
    string? Occupation,
    decimal? AnnualIncome,
    string? Address,
    string? City,
    bool IsEmergencyContact
);

public record GuardianSummaryDto(
    Guid Id,
    Guid OrganizationId,
    string FullName,
    GuardianRelationshipType RelationshipType,
    string Phone,
    string? Email,
    string? Occupation,
    int LinkedStudentsCount
);

public record LinkedStudentDto(
    Guid StudentId,
    string FullName,
    string AdmissionNumber,
    string CurrentGrade,
    string Relationship,
    bool IsPrimaryContact
);

public record GuardianDetailsDto(
    Guid Id,
    Guid OrganizationId,
    Guid? BranchId,
    string FirstName,
    string LastName,
    string FullName,
    GuardianRelationshipType RelationshipType,
    string? NationalId,
    string? Email,
    string Phone,
    string? AlternatePhone,
    string? Occupation,
    decimal? AnnualIncome,
    string? Address,
    string? City,
    bool IsEmergencyContact,
    IReadOnlyList<LinkedStudentDto> LinkedStudents,
    DateTime CreatedAtUtc
);

public record LinkStudentToGuardianRequestDto(
    Guid StudentId,
    string Relationship = "Guardian",
    bool IsPrimaryContact = true,
    bool IsAuthorizedToPickup = true
);

public record ProvisionParentUserRequestDto(
    string? Email = null,
    string? Password = null
);

public record ParentChildSummaryDto(
    Guid StudentId,
    string FullName,
    string AdmissionNumber,
    string? RollNumber,
    string CurrentGrade,
    string? Section,
    string Status,
    Guid BranchId,
    string? BranchName,
    string Relationship,
    bool IsPrimaryContact
);

public record ParentDashboardDto(
    ParentChildSummaryDto? SelectedChild,
    IReadOnlyList<ParentChildSummaryDto> Children,
    double AttendancePercentage,
    int TotalAttendanceRecords,
    int PresentDays,
    int AbsentDays,
    decimal OutstandingFees,
    int UpcomingExamsCount,
    int ActiveAssignmentsCount,
    IReadOnlyList<StudentAnnouncementDto> RecentAnnouncements
);

public record ParentGuardianProfileDto(
    Guid GuardianId,
    Guid? UserId,
    string FullName,
    string Phone,
    string? Email,
    string? AlternatePhone,
    string? Occupation,
    string? Address,
    string? City,
    bool IsEmergencyContact,
    IReadOnlyList<ParentChildSummaryDto> LinkedChildren
);

public record UpdateParentContactRequestDto(
    string? Phone = null,
    string? AlternatePhone = null,
    string? Address = null,
    string? City = null,
    string? Occupation = null
);

