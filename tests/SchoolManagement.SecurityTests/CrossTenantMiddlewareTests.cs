using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using SchoolManagement.Api.Middleware;
using SchoolManagement.Application.Common.Exceptions;
using SchoolManagement.Application.Common.Models;
using SchoolManagement.Domain.Enums;
using Xunit;

namespace SchoolManagement.SecurityTests;

public class CrossTenantMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_NonAdminSpoofingOtherTenantHeader_ShouldThrowCrossTenantAccessException()
    {
        // Arrange
        var userOrgId = Guid.NewGuid();
        var spoofedOrgId = Guid.NewGuid();

        var claims = new List<Claim>
        {
            new("tenant_id", userOrgId.ToString()),
            new("role_type", nameof(UserRoleType.Teacher))
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        var context = new DefaultHttpContext { User = principal };
        context.Request.Headers["X-Tenant-Id"] = spoofedOrgId.ToString();

        var tenantContext = new TenantContext();
        var middleware = new TenantResolutionMiddleware(next: (innerContext) => Task.CompletedTask);

        // Act
        Func<Task> act = async () => await middleware.InvokeAsync(context, tenantContext);

        // Assert
        await act.Should().ThrowAsync<CrossTenantAccessException>();
    }

    [Fact]
    public async Task InvokeAsync_SuperAdminSupplyingTenantHeader_ShouldAllowAndSetTenantContext()
    {
        // Arrange
        var targetOrgId = Guid.NewGuid();

        var claims = new List<Claim>
        {
            new("role_type", nameof(UserRoleType.SuperAdmin))
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        var context = new DefaultHttpContext { User = principal };
        context.Request.Headers["X-Tenant-Id"] = targetOrgId.ToString();

        var tenantContext = new TenantContext();
        var middleware = new TenantResolutionMiddleware(next: (innerContext) => Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(context, tenantContext);

        // Assert
        tenantContext.OrganizationId.Should().Be(targetOrgId);
        tenantContext.IsSuperAdmin.Should().BeTrue();
    }
}
