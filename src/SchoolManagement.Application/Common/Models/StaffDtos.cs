using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Application.Common.Models;

public record CreateStaffRequestDto(
    Guid? BranchId,
    string? EmployeeCode,
    string FirstName,
    string LastName,
    string Email,
    string? PhoneNumber,
    string Designation,
    string Department,
    string? Qualification,
    string? Specialization,
    EmploymentType EmploymentType,
    DateTime? JoiningDate,
    decimal? BasicSalary,
    string? EmergencyContactName,
    string? EmergencyContactPhone,
    string? NationalId,
    string? InitialPassword,
    IReadOnlyList<Guid>? RoleIds
);

public record UpdateStaffRequestDto(
    Guid? BranchId,
    string FirstName,
    string LastName,
    string? PhoneNumber,
    string Designation,
    string Department,
    string? Qualification,
    string? Specialization,
    EmploymentType EmploymentType,
    StaffStatus Status,
    decimal? BasicSalary,
    string? EmergencyContactName,
    string? EmergencyContactPhone,
    string? NationalId
);

public record StaffSummaryDto(
    Guid Id,
    Guid UserId,
    Guid OrganizationId,
    Guid? BranchId,
    string? BranchName,
    string EmployeeCode,
    string FullName,
    string Email,
    string? PhoneNumber,
    string Designation,
    string Department,
    EmploymentType EmploymentType,
    StaffStatus Status,
    DateTime JoiningDate
);

public record StaffDetailsDto(
    Guid Id,
    Guid UserId,
    Guid OrganizationId,
    Guid? BranchId,
    string? BranchName,
    string EmployeeCode,
    string FirstName,
    string LastName,
    string FullName,
    string Email,
    string? PhoneNumber,
    string Designation,
    string Department,
    string? Qualification,
    string? Specialization,
    EmploymentType EmploymentType,
    StaffStatus Status,
    DateTime JoiningDate,
    decimal? BasicSalary,
    string? EmergencyContactName,
    string? EmergencyContactPhone,
    string? NationalId,
    IReadOnlyList<string> Roles,
    DateTime CreatedAtUtc
);

public record TransferStaffBranchRequestDto(
    Guid NewBranchId,
    string? Reason = null
);

public record SubmitStaffLeaveRequestDto(
    string LeaveType,
    DateTime StartDate,
    DateTime EndDate,
    string Reason,
    string? AttachmentUrl = null
);

public record ReviewStaffLeaveRequestDto(
    LeaveStatus Status,
    string? DecisionRemarks = null
);

public record StaffLeaveDto(
    Guid Id,
    Guid StaffProfileId,
    string EmployeeCode,
    string StaffName,
    string LeaveType,
    DateTime StartDate,
    DateTime EndDate,
    int TotalDays,
    string Reason,
    string? AttachmentUrl,
    LeaveStatus Status,
    DateTime CreatedAtUtc,
    string? ApprovedByName,
    string? DecisionRemarks,
    DateTime? DecisionDateUtc
);

