using FluentAssertions;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Enums;
using Xunit;

namespace SchoolManagement.UnitTests;

public class BaseEntityTests
{
    private class TestEntity : BaseEntity
    {
        public string Name { get; set; } = string.Empty;
    }

    [Fact]
    public void BaseEntity_Initialization_ShouldSetDefaultValues()
    {
        // Act
        var entity = new TestEntity { Name = "Test" };

        // Assert
        entity.Id.Should().NotBeEmpty();
        entity.CreatedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
        entity.CreatedBy.Should().Be("System");
        entity.IsDeleted.Should().BeFalse();
        entity.SyncStatus.Should().Be(SyncStatus.Synced);
        entity.SyncVersion.Should().Be(1);
    }

    [Fact]
    public void MarkUpdated_ShouldUpdateTimestampAndIncrementSyncVersion()
    {
        // Arrange
        var entity = new TestEntity();
        long initialVersion = entity.SyncVersion;

        // Act
        entity.MarkUpdated("admin@school.edu");

        // Assert
        entity.UpdatedAtUtc.Should().NotBeNull();
        entity.UpdatedAtUtc.Value.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
        entity.UpdatedBy.Should().Be("admin@school.edu");
        entity.SyncVersion.Should().Be(initialVersion + 1);
    }

    [Fact]
    public void MarkDeleted_ShouldSetIsDeletedAndIncrementSyncVersion()
    {
        // Arrange
        var entity = new TestEntity();
        long initialVersion = entity.SyncVersion;

        // Act
        entity.MarkDeleted("admin@school.edu");

        // Assert
        entity.IsDeleted.Should().BeTrue();
        entity.UpdatedAtUtc.Should().NotBeNull();
        entity.UpdatedBy.Should().Be("admin@school.edu");
        entity.SyncVersion.Should().Be(initialVersion + 1);
    }
}
