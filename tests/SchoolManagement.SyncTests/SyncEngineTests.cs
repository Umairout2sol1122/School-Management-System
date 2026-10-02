using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using SchoolManagement.Application.Common.Models;
using SchoolManagement.Client.Core.Persistence;
using SchoolManagement.Client.Core.Services;
using SchoolManagement.Client.Core.Sync;
using SchoolManagement.Domain.Enums;
using Xunit;

namespace SchoolManagement.SyncTests;

public class SyncEngineTests
{
    private readonly Guid _orgId = Guid.NewGuid();
    private readonly Mock<IApiClient> _apiClientMock = new();

    private LocalDbContext CreateLocalDb()
    {
        var options = new DbContextOptionsBuilder<LocalDbContext>()
            .UseInMemoryDatabase(databaseName: $"LocalDb_{Guid.NewGuid()}")
            .Options;
        return new LocalDbContext(options);
    }

    [Fact]
    public async Task QueueLocalChangeAsync_ShouldCreatePendingQueueItem()
    {
        // Arrange
        using var localDb = CreateLocalDb();
        var syncEngine = new SyncEngine(localDb, _apiClientMock.Object);

        // Act
        var clientSyncId = await syncEngine.QueueLocalChangeAsync(
            _orgId,
            "Student",
            Guid.NewGuid(),
            SyncAction.Insert,
            "{\"Name\":\"John Doe\"}");

        // Assert
        clientSyncId.Should().NotBeEmpty();

        var queuedItem = await localDb.SyncQueue.FirstOrDefaultAsync(q => q.ClientSyncId == clientSyncId);
        queuedItem.Should().NotBeNull();
        queuedItem!.SyncStatus.Should().Be(SyncStatus.Pending);
        queuedItem.OrganizationId.Should().Be(_orgId);
        queuedItem.EntityName.Should().Be("Student");

        int count = await syncEngine.GetPendingQueueCountAsync(_orgId);
        count.Should().Be(1);
    }

    [Fact]
    public async Task SyncNowAsync_WhenServerIsOnline_ShouldPushBatchAndMarkSynced()
    {
        // Arrange
        using var localDb = CreateLocalDb();
        var syncEngine = new SyncEngine(localDb, _apiClientMock.Object);

        var clientSyncId = await syncEngine.QueueLocalChangeAsync(
            _orgId,
            "Student",
            Guid.NewGuid(),
            SyncAction.Insert,
            "{\"Name\":\"Alice Smith\"}");

        _apiClientMock.Setup(a => a.CheckServerHealthAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _apiClientMock.Setup(a => a.PushSyncBatchAsync(It.IsAny<SyncPushRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SyncPushResponseDto(true, new List<SyncItemAckDto>
            {
                new(clientSyncId, SyncStatus.Synced, 1, null)
            }));

        _apiClientMock.Setup(a => a.PullSyncDeltaAsync(_orgId, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SyncPullResponseDto(true, 1, Array.Empty<SyncPullItemDto>()));

        // Act
        bool syncResult = await syncEngine.SyncNowAsync(_orgId);

        // Assert
        syncResult.Should().BeTrue();

        var item = await localDb.SyncQueue.FirstOrDefaultAsync(q => q.ClientSyncId == clientSyncId);
        item.Should().NotBeNull();
        item!.SyncStatus.Should().Be(SyncStatus.Synced);

        int pendingCount = await syncEngine.GetPendingQueueCountAsync(_orgId);
        pendingCount.Should().Be(0);
    }

    [Fact]
    public async Task SyncNowAsync_WhenServerIsOffline_ShouldRetainPendingStatusAndReturnFalse()
    {
        // Arrange
        using var localDb = CreateLocalDb();
        var syncEngine = new SyncEngine(localDb, _apiClientMock.Object);

        var clientSyncId = await syncEngine.QueueLocalChangeAsync(
            _orgId,
            "Student",
            Guid.NewGuid(),
            SyncAction.Insert,
            "{\"Name\":\"Bob Marley\"}");

        _apiClientMock.Setup(a => a.CheckServerHealthAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false); // Server offline

        // Act
        bool syncResult = await syncEngine.SyncNowAsync(_orgId);

        // Assert
        syncResult.Should().BeFalse();

        var item = await localDb.SyncQueue.FirstOrDefaultAsync(q => q.ClientSyncId == clientSyncId);
        item!.SyncStatus.Should().Be(SyncStatus.Pending);

        int pendingCount = await syncEngine.GetPendingQueueCountAsync(_orgId);
        pendingCount.Should().Be(1);
    }
}
