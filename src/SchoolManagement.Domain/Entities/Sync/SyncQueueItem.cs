using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Domain.Entities.Sync;

public class SyncQueueItem : BaseEntity
{
    public Guid OrganizationId { get; set; }
    public Guid ClientSyncId { get; set; } = Guid.NewGuid();
    public string EntityName { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public SyncAction Action { get; set; }
    public string PayloadJson { get; set; } = string.Empty;
    public new SyncStatus SyncStatus { get; set; } = SyncStatus.Pending;
    public int RetryCount { get; set; } = 0;
    public string? ErrorMessage { get; set; }
    public DateTime? ProcessedAtUtc { get; set; }
}
