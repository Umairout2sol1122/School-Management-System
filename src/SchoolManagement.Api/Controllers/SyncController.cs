using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.Common.Models;
using SchoolManagement.Domain.Entities.Sync;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SyncController : BaseApiController
{
    private readonly IApplicationDbContext _context;
    private readonly IAuditService _auditService;

    public SyncController(IApplicationDbContext context, IAuditService auditService)
    {
        _context = context;
        _auditService = auditService;
    }

    [HttpPost("push")]
    public async Task<IActionResult> Push([FromBody] SyncPushRequestDto request, CancellationToken cancellationToken)
    {
        // Enforce tenant isolation: User can only push for their own organization unless SuperAdmin
        if (!CurrentUserService.IsSuperAdmin && CurrentUserService.OrganizationId != request.OrganizationId)
        {
            return ApiError("Unauthorized organization push.", StatusCodes.Status403Forbidden);
        }

        var acks = new List<SyncItemAckDto>();

        foreach (var item in request.Items)
        {
            // Check idempotency: Have we already processed this ClientSyncId?
            var existingQueueItem = await _context.SyncQueue
                .FirstOrDefaultAsync(q => q.ClientSyncId == item.ClientSyncId, cancellationToken);

            if (existingQueueItem != null)
            {
                // Already processed, return existing status
                acks.Add(new SyncItemAckDto(
                    item.ClientSyncId,
                    existingQueueItem.SyncStatus,
                    existingQueueItem.SyncVersion,
                    null));
                continue;
            }

            // Create new queue entry
            var queueItem = new SyncQueueItem
            {
                Id = Guid.NewGuid(),
                OrganizationId = request.OrganizationId,
                ClientSyncId = item.ClientSyncId,
                EntityName = item.EntityName,
                EntityId = item.EntityId,
                Action = item.Action,
                PayloadJson = item.PayloadJson,
                SyncStatus = SyncStatus.Synced,
                ProcessedAtUtc = DateTime.UtcNow
            };

            _context.SyncQueue.Add(queueItem);

            acks.Add(new SyncItemAckDto(
                item.ClientSyncId,
                SyncStatus.Synced,
                queueItem.SyncVersion,
                null));
        }

        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            AuditAction.SyncPush, "Sync", "SyncQueue", null,
            $"Processed sync push batch of {request.Items.Count} items for Organization '{request.OrganizationId}'",
            organizationId: request.OrganizationId,
            userId: CurrentUserService.UserId,
            userEmail: CurrentUserService.Email,
            isOfflineSync: true,
            cancellationToken: cancellationToken);

        return ApiSuccess(new SyncPushResponseDto(true, acks));
    }

    [HttpGet("pull")]
    public async Task<IActionResult> Pull(
        [FromQuery] Guid organizationId,
        [FromQuery] long sinceVersion = 0,
        CancellationToken cancellationToken = default)
    {
        if (!CurrentUserService.IsSuperAdmin && CurrentUserService.OrganizationId != organizationId)
        {
            return ApiError("Unauthorized organization pull.", StatusCodes.Status403Forbidden);
        }

        var queueItems = await _context.SyncQueue
            .Where(q => q.OrganizationId == organizationId && q.SyncVersion > sinceVersion && q.SyncStatus == SyncStatus.Synced)
            .OrderBy(q => q.SyncVersion)
            .Take(100)
            .ToListAsync(cancellationToken);

        long latestVersion = queueItems.Any() ? queueItems.Max(q => q.SyncVersion) : sinceVersion;

        var pullItems = queueItems.Select(q => new SyncPullItemDto(
            q.EntityName,
            q.EntityId,
            q.Action.ToString(),
            q.PayloadJson,
            q.SyncVersion,
            q.ProcessedAtUtc ?? q.CreatedAtUtc
        )).ToList();

        return ApiSuccess(new SyncPullResponseDto(true, latestVersion, pullItems));
    }
}
