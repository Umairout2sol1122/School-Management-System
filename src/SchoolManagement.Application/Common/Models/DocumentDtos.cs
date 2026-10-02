using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Application.Common.Models;

public record StudentDocumentItemDto(
    Guid Id,
    Guid StudentId,
    string StudentName,
    string AdmissionNumber,
    string CurrentGrade,
    Guid? BranchId,
    string? BranchName,
    string Title,
    StudentDocumentType DocumentType,
    string DocumentTypeDisplay,
    string FileUrl,
    string FileName,
    long FileSizeBytes,
    string? Description,
    DateTime CreatedAtUtc,
    string? UploadedBy
);

public record UploadDocumentRequestDto(
    Guid StudentId,
    string Title,
    StudentDocumentType DocumentType,
    string FileName,
    string FileUrl,
    long FileSizeBytes,
    string? Description = null
);
