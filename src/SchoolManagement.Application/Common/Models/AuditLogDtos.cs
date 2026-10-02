using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Application.Common.Models;

public record AuditLogSummaryDto(
    Guid Id,
    DateTime TimestampUtc,
    AuditAction Action,
    string Module,
    string EntityName,
    string? EntityId,
    string Details,
    string? IpAddress,
    string? CorrelationId,
    Guid? OrganizationId,
    string? OrganizationName,
    string UserEmail
);

public record AuditLogFilterRequestDto(
    DateTime? FromDateUtc,
    DateTime? ToDateUtc,
    AuditAction? Action,
    string? Module,
    Guid? OrganizationId,
    string? Search,
    int PageNumber = 1,
    int PageSize = 50
);
