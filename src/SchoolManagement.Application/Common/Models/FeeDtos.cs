using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Application.Common.Models;

public record FeeInvoiceSummaryDto(
    Guid Id,
    Guid OrganizationId,
    Guid? BranchId,
    string? BranchName,
    Guid StudentId,
    string StudentName,
    string AdmissionNumber,
    string CurrentGrade,
    string? Section,
    string InvoiceNumber,
    string Title,
    DateTime IssueDate,
    DateTime DueDate,
    decimal TotalAmount,
    decimal PaidAmount,
    decimal Balance,
    FeeStatus Status,
    bool IsOverdue
);

public record FeeInvoiceDetailDto(
    Guid Id,
    Guid OrganizationId,
    Guid? BranchId,
    string? BranchName,
    Guid StudentId,
    string StudentName,
    string AdmissionNumber,
    string CurrentGrade,
    string? Section,
    string InvoiceNumber,
    string Title,
    DateTime IssueDate,
    DateTime DueDate,
    decimal TotalAmount,
    decimal PaidAmount,
    decimal Balance,
    FeeStatus Status,
    bool IsOverdue,
    IReadOnlyList<FeePaymentDetailDto> Payments
);

public record FeePaymentDetailDto(
    Guid Id,
    Guid FeeInvoiceId,
    string ReceiptNumber,
    decimal AmountPaid,
    DateTime PaymentDate,
    string PaymentMethod,
    string? TransactionReference,
    Guid? ReceivedByUserId,
    string? ReceivedByUserName
);

public record BulkGenerateFeeInvoicesRequest(
    string Title,
    decimal Amount,
    DateTime DueDate,
    string? ClassGrade = null,
    string? Section = null,
    Guid? BranchId = null,
    decimal? DiscountPercentage = null,
    string? Remarks = null
);

public record BulkGenerateFeeInvoicesResultDto(
    int InvoicesGenerated,
    decimal TotalBilledAmount,
    string TargetGrade,
    string? TargetSection,
    IReadOnlyList<string> GeneratedInvoiceNumbers
);

public record RefundFeePaymentRequest(
    decimal RefundAmount,
    string Reason,
    string? RefundMethod = "Cash"
);

public record FeeDefaulterDto(
    Guid StudentId,
    string AdmissionNumber,
    string FullName,
    string CurrentGrade,
    string? Section,
    Guid? BranchId,
    string? BranchName,
    string? GuardianName,
    string? GuardianPhone,
    int UnpaidInvoiceCount,
    decimal TotalOverdueAmount,
    DateTime EarliestDueDate,
    int DaysOverdue
);

public record FeeFinancialSummaryDto(
    decimal TotalBilled,
    decimal TotalCollected,
    decimal TotalOutstanding,
    int TotalInvoicesCount,
    int PaidInvoicesCount,
    int PartiallyPaidInvoicesCount,
    int UnpaidInvoicesCount,
    int DefaultersCount,
    decimal OverdueAmount
);
