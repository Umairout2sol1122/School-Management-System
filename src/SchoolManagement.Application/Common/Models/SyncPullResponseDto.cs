namespace SchoolManagement.Application.Common.Models;

public record SyncPullItemDto(
    string EntityName,
    Guid EntityId,
    string Action,
    string PayloadJson,
    long SyncVersion,
    DateTime UpdatedAtUtc
);

public record SyncPullResponseDto(
    bool Success,
    long LatestSyncVersion,
    IReadOnlyList<SyncPullItemDto> Items,
    string? ErrorMessage = null
);
