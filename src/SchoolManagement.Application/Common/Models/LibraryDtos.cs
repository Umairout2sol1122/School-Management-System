using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Application.Common.Models;

public record BookDto(
    Guid Id,
    Guid OrganizationId,
    Guid? BranchId,
    string? BranchName,
    string Title,
    string Author,
    string? Isbn,
    string? Publisher,
    string Category,
    int TotalCopies,
    int AvailableCopies,
    string? RackNumber,
    bool IsActive
);

public record CreateBookRequest(
    string Title,
    string Author,
    string? Isbn = null,
    string? Publisher = null,
    string Category = "General",
    int TotalCopies = 1,
    string? RackNumber = null,
    Guid? BranchId = null
);

public record UpdateBookRequest(
    string Title,
    string Author,
    string? Isbn,
    string? Publisher,
    string Category,
    int TotalCopies,
    string? RackNumber,
    bool IsActive
);

public record BookIssueDto(
    Guid Id,
    Guid BookId,
    string BookTitle,
    string BookAuthor,
    Guid? StudentId,
    string? StudentName,
    string? AdmissionNumber,
    Guid? StaffProfileId,
    string? StaffName,
    string? EmployeeCode,
    DateTime IssueDate,
    DateTime DueDate,
    DateTime? ReturnDate,
    decimal FineAmount,
    BookIssueStatus Status,
    string? Remarks
);

public record IssueBookRequest(
    Guid BookId,
    Guid? StudentId = null,
    Guid? StaffProfileId = null,
    int DueDays = 14,
    string? Remarks = null
);

public record ReturnBookRequest(
    decimal? FineAmount = 0m,
    BookIssueStatus Status = BookIssueStatus.Returned,
    string? Remarks = null
);
