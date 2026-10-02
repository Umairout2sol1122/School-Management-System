using Serilog;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Domain.Entities.Audit;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Infrastructure.Services;

public class AuditService : IAuditService
{
    private readonly IApplicationDbContext _context;

    public AuditService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task LogAsync(
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
        CancellationToken cancellationToken = default)
    {
        var auditLog = new AuditLog
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            BranchId = branchId,
            UserId = userId,
            UserEmail = userEmail ?? "System",
            Action = action,
            Module = module,
            EntityName = entityName,
            EntityId = entityId,
            Details = details,
            IsOfflineSync = isOfflineSync,
            TimestampUtc = DateTime.UtcNow
        };

        // Write to structured application logger
        Log.ForContext("OrganizationId", organizationId)
           .ForContext("BranchId", branchId)
           .ForContext("UserId", userId)
           .ForContext("UserEmail", userEmail)
           .ForContext("AuditAction", action.ToString())
           .ForContext("Module", module)
           .ForContext("EntityName", entityName)
           .ForContext("EntityId", entityId)
           .ForContext("IsOfflineSync", isOfflineSync)
           .Information("Audit: [{Module}] {Action} on {EntityName} ({EntityId}): {Details}",
                module, action, entityName, entityId, details);

        try
        {
            _context.AuditLogs.Add(auditLog);
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to persist audit log to database for action {Action} on {EntityName}", action, entityName);
        }
    }
}
