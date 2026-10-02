using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Application.Common.Models;

public record SyncPushItemDto(
    Guid ClientSyncId,
    string EntityName,
    Guid EntityId,
    SyncAction Action,
    string PayloadJson,
    long ClientTimestamp
);

public record SyncPushRequestDto(
    Guid OrganizationId,
    IReadOnlyList<SyncPushItemDto> Items
);

public record SyncItemAckDto(
    Guid ClientSyncId,
    SyncStatus Status,
    long? ServerSyncVersion,
    string? ErrorMessage
);

public record SyncPushResponseDto(
    bool Success,
    IReadOnlyList<SyncItemAckDto> AcknowledgedItems,
    string? ErrorMessage = null
);
