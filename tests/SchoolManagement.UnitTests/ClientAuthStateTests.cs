using FluentAssertions;
using SchoolManagement.Application.Common.Models;
using SchoolManagement.Client.Core.Auth;
using SchoolManagement.Domain.Constants;
using SchoolManagement.Domain.Enums;
using Xunit;

namespace SchoolManagement.UnitTests;

public class ClientAuthStateTests
{
    [Fact]
    public void ClientAuthState_Initial_ShouldNotBeAuthenticated()
    {
        // Arrange
        var state = new ClientAuthState();

        // Assert
        state.IsAuthenticated.Should().BeFalse();
        state.CurrentUser.Should().BeNull();
        state.AccessToken.Should().BeNull();
    }

    [Fact]
    public void SetAuthenticated_ShouldUpdateStateAndTriggerEvent()
    {
        // Arrange
        var state = new ClientAuthState();
        bool eventFired = false;
        state.OnAuthStateChanged += () => eventFired = true;

        var user = new UserDto(
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            "testuser",
            "test@school.edu",
            "Test",
            "User",
            "Test User",
            UserRoleType.Teacher,
            true,
            new[] { Permissions.AttendanceMark }
        );

        // Act
        state.SetAuthenticated(user, "test_access_token", "test_refresh_token", DateTime.UtcNow.AddHours(1));

        // Assert
        state.IsAuthenticated.Should().BeTrue();
        state.CurrentUser.Should().Be(user);
        state.AccessToken.Should().Be("test_access_token");
        state.HasPermission(Permissions.AttendanceMark).Should().BeTrue();
        state.HasPermission(Permissions.FeesCollectPayment).Should().BeFalse();
        eventFired.Should().BeTrue();
    }

    [Fact]
    public void SuperAdmin_HasPermission_ShouldAlwaysReturnTrue()
    {
        // Arrange
        var state = new ClientAuthState();
        var superAdmin = new UserDto(
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            "superadmin",
            "super@platform.com",
            "Super",
            "Admin",
            "Super Admin",
            UserRoleType.SuperAdmin,
            true,
            Array.Empty<string>()
        );

        // Act
        state.SetAuthenticated(superAdmin, "token", "refresh", DateTime.UtcNow.AddHours(1));

        // Assert
        state.HasPermission("Any.Random.Permission").Should().BeTrue();
    }
}
