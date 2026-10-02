using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Application.Common.Models;

public record CreateStudentRequestDto(
    Guid? BranchId,
    string? AdmissionNumber,
    string? RollNumber,
    string FirstName,
    string LastName,
    DateTime? DateOfBirth,
    Gender Gender,
    string? BloodGroup,
    string? NationalIdOrBForm,
    DateTime? AdmissionDate,
    string CurrentGrade = "General",
    string? Section = null,
    string? EmergencyContactName = null,
    string? EmergencyContactPhone = null,
    string? EmergencyContactRelationship = null,
    string? Address = null,
    string? City = null,
    string? MedicalNotes = null,
    CreateGuardianRequestDto? InitialGuardian = null,
    string? GuardianRelationship = null,
    bool IsPrimaryGuardian = true
);

public record UpdateStudentRequestDto(
    Guid? BranchId,
    string? RollNumber,
    string FirstName,
    string LastName,
    DateTime? DateOfBirth,
    Gender Gender,
    string? BloodGroup,
    string? NationalIdOrBForm,
    StudentStatus Status,
    string CurrentGrade,
    string? Section,
    string? EmergencyContactName,
    string? EmergencyContactPhone,
    string? EmergencyContactRelationship,
    string? Address,
    string? City,
    string? MedicalNotes
);

public record StudentSummaryDto(
    Guid Id,
    Guid OrganizationId,
    Guid? BranchId,
    string? BranchName,
    string AdmissionNumber,
    string? RollNumber,
    string FullName,
    string CurrentGrade,
    string? Section,
    Gender Gender,
    StudentStatus Status,
    DateTime AdmissionDate,
    string? PrimaryGuardianName,
    string? PrimaryGuardianPhone
);

public record StudentDetailsDto(
    Guid Id,
    Guid OrganizationId,
    Guid? BranchId,
    string? BranchName,
    string AdmissionNumber,
    string? RollNumber,
    string FirstName,
    string LastName,
    string FullName,
    DateTime DateOfBirth,
    Gender Gender,
    string? BloodGroup,
    string? NationalIdOrBForm,
    StudentStatus Status,
    DateTime AdmissionDate,
    string CurrentGrade,
    string? Section,
    string? EmergencyContactName,
    string? EmergencyContactPhone,
    string? EmergencyContactRelationship,
    string? Address,
    string? City,
    string? MedicalNotes,
    string? ProfilePictureUrl,
    IReadOnlyList<StudentGuardianDto> Guardians,
    DateTime CreatedAtUtc
);

public record AssignGuardianRequestDto(
    Guid GuardianId,
    string Relationship,
    bool IsPrimaryContact,
    bool IsAuthorizedToPickup
);

public record StudentGuardianDto(
    Guid GuardianId,
    string FullName,
    string Relationship,
    string Phone,
    string? Email,
    bool IsPrimaryContact,
    bool IsAuthorizedToPickup
);

public record TransferBranchRequestDto(
    Guid TargetBranchId,
    string? Reason = null
);

public record TransferClassRequestDto(
    string TargetGrade,
    string? TargetSection = null,
    string? Reason = null
);

public record UpdateStudentStatusRequestDto(
    StudentStatus Status,
    string? Remarks = null,
    bool ForceBypassClearance = false
);

public record SubmitAdmissionApplicationRequest(
    string FirstName,
    string LastName,
    DateTime DateOfBirth,
    Gender Gender,
    string DesiredGrade,
    string? GuardianName = null,
    string? GuardianPhone = null,
    string? GuardianEmail = null,
    string? GuardianRelationship = null,
    string? Address = null,
    Guid? BranchId = null
);

public record ReviewApplicationRequest(
    string? ReviewNotes
);

public record RejectApplicationRequest(
    string RejectionReason
);

public record ApproveAndEnrollApplicationRequest(
    string? Section = null,
    decimal? InitialTuitionFee = null,
    DateTime? FeeDueDate = null
);

public record AdmissionApplicationDto(
    Guid Id,
    Guid OrganizationId,
    Guid? BranchId,
    string? BranchName,
    string ApplicationNumber,
    string FirstName,
    string LastName,
    string FullName,
    DateTime DateOfBirth,
    Gender Gender,
    string DesiredGrade,
    string? GuardianName,
    string? GuardianPhone,
    string? GuardianEmail,
    string? GuardianRelationship,
    string? Address,
    ApplicationStatus Status,
    string StatusName,
    string? ReviewNotes,
    string? RejectionReason,
    Guid? EnrolledStudentId,
    DateTime? DecisionDateUtc,
    DateTime CreatedAtUtc
);

