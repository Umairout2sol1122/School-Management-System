using SchoolManagement.Application.Common.Models;
using SchoolManagement.Client.Core.Auth;
using SchoolManagement.Domain.Constants;
using SchoolManagement.Domain.Enums;
using Xunit;

namespace SchoolManagement.UnitTests;

public class ClientAuthStateRbacTests
{
    [Fact]
    public void HasPermission_RegularUserWithPermission_ShouldReturnTrue()
    {
        var authState = new ClientAuthState();
        var user = new UserDto(
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            "teacher1",
            "teacher@test.com",
            "Test",
            "Teacher",
            "Test Teacher",
            UserRoleType.Teacher,
            true,
            [Permissions.AttendanceMark, Permissions.AttendanceView]
        );

        authState.SetAuthenticated(user, "token", "refresh", DateTime.UtcNow.AddHours(1));

        Assert.True(authState.HasPermission(Permissions.AttendanceMark));
        Assert.True(authState.HasPermission(Permissions.AttendanceView));
        Assert.False(authState.HasPermission(Permissions.UsersCreate));
    }

    [Fact]
    public void HasPermission_SuperAdmin_ShouldReturnTrueForAnyPermission()
    {
        var authState = new ClientAuthState();
        var user = new UserDto(
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            "superadmin",
            "superadmin@test.com",
            "Super",
            "Admin",
            "Super Admin",
            UserRoleType.SuperAdmin,
            true,
            [] // Empty permissions array
        );

        authState.SetAuthenticated(user, "token", "refresh", DateTime.UtcNow.AddHours(1));

        Assert.True(authState.HasPermission(Permissions.OrganizationsManage));
        Assert.True(authState.HasPermission(Permissions.UsersCreate));
        Assert.True(authState.HasPermission("Any.NonExistent.Permission"));
        Assert.True(authState.IsSuperAdmin);
    }

    [Fact]
    public void HasAnyPermission_WhenUserHasAtLeastOne_ShouldReturnTrue()
    {
        var authState = new ClientAuthState();
        var user = new UserDto(
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            "staff1",
            "staff@test.com",
            "Staff",
            "Member",
            "Staff Member",
            UserRoleType.Staff,
            true,
            [Permissions.StudentsView]
        );

        authState.SetAuthenticated(user, "token", "refresh", DateTime.UtcNow.AddHours(1));

        bool result = authState.HasAnyPermission(Permissions.UsersCreate, Permissions.StudentsView, Permissions.FeesRefund);

        Assert.True(result);
    }

    [Fact]
    public void HasAllPermissions_WhenUserLacksOne_ShouldReturnFalse()
    {
        var authState = new ClientAuthState();
        var user = new UserDto(
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            "staff1",
            "staff@test.com",
            "Staff",
            "Member",
            "Staff Member",
            UserRoleType.Staff,
            true,
            [Permissions.StudentsView]
        );

        authState.SetAuthenticated(user, "token", "refresh", DateTime.UtcNow.AddHours(1));

        bool result = authState.HasAllPermissions(Permissions.StudentsView, Permissions.StudentsEdit);

        Assert.False(result);
    }

    [Fact]
    public void RoleChecks_ShouldReflectAssignedRole()
    {
        var authState = new ClientAuthState();
        var user = new UserDto(
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            "orgadmin",
            "orgadmin@test.com",
            "Org",
            "Admin",
            "Org Admin",
            UserRoleType.OrganizationAdmin,
            true,
            []
        );

        authState.SetAuthenticated(user, "token", "refresh", DateTime.UtcNow.AddHours(1));

        Assert.True(authState.IsOrganizationAdmin);
        Assert.False(authState.IsSuperAdmin);
        Assert.True(authState.IsInRole(UserRoleType.OrganizationAdmin));
        Assert.False(authState.IsInRole(UserRoleType.Teacher));
    }
}
