using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using SchoolManagement.Api.Authorization;
using SchoolManagement.Domain.Constants;
using SchoolManagement.Domain.Enums;
using Xunit;

namespace SchoolManagement.UnitTests;

public class PermissionAuthorizationHandlerTests
{
    private readonly PermissionAuthorizationHandler _handler;

    public PermissionAuthorizationHandlerTests()
    {
        _handler = new PermissionAuthorizationHandler();
    }

    [Fact]
    public async Task HandleAsync_UnauthenticatedUser_ShouldNotSucceedRequirement()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity());
        var requirement = new PermissionRequirement(Permissions.UsersView);
        var context = new AuthorizationHandlerContext([requirement], principal, null);

        await _handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task HandleAsync_SuperAdminRole_ShouldSucceedRequirementUnconditionally()
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, UserRoleType.SuperAdmin.ToString())
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        var requirement = new PermissionRequirement(Permissions.UsersCreate);
        var context = new AuthorizationHandlerContext([requirement], principal, null);

        await _handler.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task HandleAsync_UserWithPermissionClaim_ShouldSucceedRequirement()
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, UserRoleType.Teacher.ToString()),
            new Claim("permission", Permissions.StudentsView)
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        var requirement = new PermissionRequirement(Permissions.StudentsView);
        var context = new AuthorizationHandlerContext([requirement], principal, null);

        await _handler.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task HandleAsync_UserWithoutRequiredPermissionClaim_ShouldNotSucceed()
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, UserRoleType.Teacher.ToString()),
            new Claim("permission", Permissions.StudentsView)
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        var requirement = new PermissionRequirement(Permissions.UsersCreate); // Requires UsersCreate, user only has StudentsView
        var context = new AuthorizationHandlerContext([requirement], principal, null);

        await _handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task HandleAsync_CaseInsensitivePermissionMatching_ShouldSucceed()
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim("permission", "users.view")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        var requirement = new PermissionRequirement("Users.View");
        var context = new AuthorizationHandlerContext([requirement], principal, null);

        await _handler.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }
}
