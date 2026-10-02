using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Application.Common.Interfaces;

public interface IAuditService
{
    Task LogAsync(
        AuditAction action,
        string module,
        string entityName,
        string? entityId,
        string details,
        Guid? organizationId = null,
        Guid? branchId = null,
        Guid? userId = null,
        string? userEmail = null,
        bool isOfflineSync = false,
        CancellationToken cancellationToken = default);
}
