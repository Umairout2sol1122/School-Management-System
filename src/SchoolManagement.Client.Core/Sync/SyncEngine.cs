using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Common.Models;
using SchoolManagement.Client.Core.Models;
using SchoolManagement.Client.Core.Persistence;
using SchoolManagement.Client.Core.Services;
using SchoolManagement.Domain.Entities.Sync;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Client.Core.Sync;

public class SyncEngine : ISyncEngine
{
    private readonly LocalDbContext _localDb;
    private readonly IApiClient _apiClient;

    public SyncEngine(LocalDbContext localDb, IApiClient apiClient)
    {
        _localDb = localDb;
        _apiClient = apiClient;
    }

    public async Task<Guid> QueueLocalChangeAsync(
        Guid organizationId,
        string entityName,
        Guid entityId,
        SyncAction action,
        string payloadJson,
        CancellationToken cancellationToken = default)
    {
        var queueItem = new SyncQueueItem
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            ClientSyncId = Guid.NewGuid(),
            EntityName = entityName,
            EntityId = entityId,
            Action = action,
            PayloadJson = payloadJson,
            SyncStatus = SyncStatus.Pending,
            CreatedAtUtc = DateTime.UtcNow
        };

        _localDb.SyncQueue.Add(queueItem);
        await _localDb.SaveChangesAsync(cancellationToken);

        return queueItem.ClientSyncId;
    }

    public async Task<int> GetPendingQueueCountAsync(Guid organizationId, CancellationToken cancellationToken = default)
    {
        return await _localDb.SyncQueue
            .CountAsync(q => q.OrganizationId == organizationId && q.SyncStatus == SyncStatus.Pending, cancellationToken);
    }

    public async Task<bool> SyncNowAsync(Guid organizationId, CancellationToken cancellationToken = default)
    {
        // Check server availability
        bool isOnline = await _apiClient.CheckServerHealthAsync(cancellationToken);
        if (!isOnline)
            return false;

        // Retrieve or initialize sync state
        var syncState = await _localDb.SyncStates
            .FirstOrDefaultAsync(s => s.OrganizationId == organizationId, cancellationToken);

        if (syncState == null)
        {
            syncState = new LocalSyncState
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                LastSyncVersion = 0,
                LastSyncAtUtc = DateTime.UtcNow
            };
            _localDb.SyncStates.Add(syncState);
            await _localDb.SaveChangesAsync(cancellationToken);
        }

        syncState.IsSyncing = true;
        await _localDb.SaveChangesAsync(cancellationToken);

        try
        {
            // Step 1: PUSH pending local changes
            var pendingItems = await _localDb.SyncQueue
                .Where(q => q.OrganizationId == organizationId && q.SyncStatus == SyncStatus.Pending)
                .OrderBy(q => q.CreatedAtUtc)
                .Take(50)
                .ToListAsync(cancellationToken);

            if (pendingItems.Any())
            {
                var pushItems = pendingItems.Select(p => new SyncPushItemDto(
                    p.ClientSyncId,
                    p.EntityName,
                    p.EntityId,
                    p.Action,
                    p.PayloadJson,
                    new DateTimeOffset(p.CreatedAtUtc).ToUnixTimeMilliseconds()
                )).ToList();

                var pushRequest = new SyncPushRequestDto(organizationId, pushItems);
                var pushResponse = await _apiClient.PushSyncBatchAsync(pushRequest, cancellationToken);

                if (pushResponse.Success)
                {
                    var acks = pushResponse.AcknowledgedItems.ToDictionary(a => a.ClientSyncId);
                    foreach (var item in pendingItems)
                    {
                        if (acks.TryGetValue(item.ClientSyncId, out var ack))
                        {
                            item.SyncStatus = ack.Status;
                            item.ErrorMessage = ack.ErrorMessage;
                            item.ProcessedAtUtc = DateTime.UtcNow;
                        }
                    }
                    await _localDb.SaveChangesAsync(cancellationToken);
                }
            }

            // Step 2: PULL remote delta changes
            var pullResponse = await _apiClient.PullSyncDeltaAsync(organizationId, syncState.LastSyncVersion, cancellationToken);
            if (pullResponse.Success)
            {
                syncState.LastSyncVersion = pullResponse.LatestSyncVersion;
                syncState.LastSyncAtUtc = DateTime.UtcNow;
                syncState.LastError = null;
            }

            syncState.IsSyncing = false;
            await _localDb.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            syncState.IsSyncing = false;
            syncState.LastError = ex.Message;
            await _localDb.SaveChangesAsync(cancellationToken);
            return false;
        }
    }
}
