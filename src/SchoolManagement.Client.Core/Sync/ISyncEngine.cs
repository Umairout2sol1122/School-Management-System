using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Client.Core.Sync;

public interface ISyncEngine
{
    Task<Guid> QueueLocalChangeAsync(
        Guid organizationId,
        string entityName,
        Guid entityId,
        SyncAction action,
        string payloadJson,
        CancellationToken cancellationToken = default);

    Task<bool> SyncNowAsync(Guid organizationId, CancellationToken cancellationToken = default);

    Task<int> GetPendingQueueCountAsync(Guid organizationId, CancellationToken cancellationToken = default);
}
