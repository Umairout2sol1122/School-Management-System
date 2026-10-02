using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Application.Common.Models;

public record CertificateDto(
    Guid Id,
    Guid OrganizationId,
    Guid? BranchId,
    Guid StudentId,
    string StudentName,
    string AdmissionNumber,
    string? RollNumber,
    string? ClassName,
    string? SectionName,
    string? FatherName,
    CertificateType CertificateType,
    string CertificateTypeName,
    string SerialNumber,
    CertificateStatus Status,
    string StatusName,
    DateTime IssueDate,
    string? IssuedByName,
    string VerificationCode,
    string? Conduct,
    string? LeavingReason,
    string? PromotedToClass,
    string? Remarks,
    string? RevocationReason,
    DateTime? RevokedAt,
    bool CanDownloadPdf = true
);

public record GenerateCertificateRequest(
    Guid StudentId,
    CertificateType CertificateType,
    string? Conduct = "Good",
    string? LeavingReason = null,
    string? PromotedToClass = null,
    string? Remarks = null,
    bool BypassClearance = false,
    string? BypassReason = null
);

public record CertificateClearanceResultDto(
    bool IsCleared,
    decimal TotalUnpaidFees,
    int UnpaidFeeCount,
    List<UnpaidInvoiceSummaryDto> UnpaidInvoices,
    int UnreturnedBookCount,
    List<UnreturnedBookSummaryDto> UnreturnedBooks,
    string ClearanceMessage
);

public record UnpaidInvoiceSummaryDto(
    Guid InvoiceId,
    string InvoiceNumber,
    decimal TotalAmount,
    decimal PaidAmount,
    decimal BalanceAmount,
    DateTime DueDate
);

public record UnreturnedBookSummaryDto(
    Guid IssueId,
    string BookTitle,
    string BookIsbn,
    DateTime IssueDate,
    DateTime DueDate,
    bool IsOverdue
);

public record CertificateVerificationDto(
    bool IsValid,
    string SerialNumber,
    string CertificateType,
    string StudentName,
    string? FatherName,
    string AdmissionNumber,
    string? ClassName,
    string OrganizationName,
    string? BranchName,
    DateTime IssueDate,
    string Status,
    bool IsRevoked,
    string? RevocationReason,
    string VerificationUrl
);

public record RevokeCertificateRequest(
    string Reason
);
