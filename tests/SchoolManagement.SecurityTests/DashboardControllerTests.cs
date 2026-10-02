using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using SchoolManagement.Api.Controllers;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.Common.Models;
using SchoolManagement.Domain.Entities.Audit;
using SchoolManagement.Domain.Entities.Security;
using SchoolManagement.Domain.Entities.Tenant;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.Infrastructure.Security;
using Xunit;

namespace SchoolManagement.SecurityTests;

public class DashboardControllerTests
{
    private ApplicationDbContext CreateInMemoryDbContext(string dbName, ITenantContext tenantContext)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        return new ApplicationDbContext(options, tenantContext);
    }

    private static TenantContext CreateTenantContext(Guid? orgId = null, Guid? branchId = null, bool isSuperAdmin = false)
    {
        var context = new TenantContext();
        context.SetTenant(orgId, branchId, isSuperAdmin);
        return context;
    }

    private void SetupControllerContext(BaseApiController controller, Guid? userId, Guid? orgId, Guid? branchId, UserRoleType role)
    {
        var claims = new List<Claim>();
        if (userId.HasValue)
            claims.Add(new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()));
        if (orgId.HasValue)
            claims.Add(new Claim("tenant_id", orgId.Value.ToString()));
        if (branchId.HasValue)
            claims.Add(new Claim("branch_id", branchId.Value.ToString()));
        claims.Add(new Claim(ClaimTypes.Role, role.ToString()));
        claims.Add(new Claim("role_type", role.ToString()));

        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext
        {
            User = principal
        };

        var currentUserServiceMock = new Mock<ICurrentUserService>();
        currentUserServiceMock.Setup(s => s.UserId).Returns(userId);
        currentUserServiceMock.Setup(s => s.OrganizationId).Returns(orgId);
        currentUserServiceMock.Setup(s => s.BranchId).Returns(branchId);
        currentUserServiceMock.Setup(s => s.RoleType).Returns(role);
        currentUserServiceMock.Setup(s => s.IsSuperAdmin).Returns(role == UserRoleType.SuperAdmin);
        currentUserServiceMock.Setup(s => s.Email).Returns("admin@p2s-academy.edu");

        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock.Setup(sp => sp.GetService(typeof(ICurrentUserService)))
            .Returns(currentUserServiceMock.Object);
        serviceProviderMock.Setup(sp => sp.GetService(typeof(ITenantContext)))
            .Returns(CreateTenantContext(orgId, branchId, role == UserRoleType.SuperAdmin));

        httpContext.RequestServices = serviceProviderMock.Object;
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
    }

    private static T? GetData<T>(IActionResult result) where T : class
    {
        var okResult = result as OkObjectResult;
        if (okResult?.Value == null) return null;
        var prop = okResult.Value.GetType().GetProperty("data")
                ?? okResult.Value.GetType().GetProperty("Data");
        return prop?.GetValue(okResult.Value) as T;
    }

    [Fact]
    public async Task GetDashboardSummary_AsSuperAdmin_ReturnsPlatformWideAggregates()
    {
        string dbName = $"DashboardSuperAdmin_{Guid.NewGuid()}";
        var tenantContext = CreateTenantContext(null, null, true);
        using var context = CreateInMemoryDbContext(dbName, tenantContext);

        var org1 = new Organization { Id = Guid.NewGuid(), Name = "Org 1", Code = "ORG-1", IsActive = true };
        var org2 = new Organization { Id = Guid.NewGuid(), Name = "Org 2", Code = "ORG-2", IsActive = true };
        var branch1 = new Branch { Id = Guid.NewGuid(), OrganizationId = org1.Id, Name = "Branch 1", Code = "B1", IsActive = true };
        var branch2 = new Branch { Id = Guid.NewGuid(), OrganizationId = org2.Id, Name = "Branch 2", Code = "B2", IsActive = true };

        var user1 = new User { Id = Guid.NewGuid(), OrganizationId = org1.Id, Username = "teacher1", Email = "t1@org1.edu", RoleType = UserRoleType.Teacher, IsActive = true };
        var user2 = new User { Id = Guid.NewGuid(), OrganizationId = org2.Id, Username = "student1", Email = "s1@org2.edu", RoleType = UserRoleType.Student, IsActive = true };

        var log = new AuditLog
        {
            Id = Guid.NewGuid(),
            Action = AuditAction.Create,
            Module = "Organizations",
            EntityName = "Organization",
            Details = "Created org",
            TimestampUtc = DateTime.UtcNow
        };

        context.Organizations.AddRange(org1, org2);
        context.Branches.AddRange(branch1, branch2);
        context.Users.AddRange(user1, user2);
        context.AuditLogs.Add(log);
        await context.SaveChangesAsync();

        var controller = new DashboardController(context);
        SetupControllerContext(controller, Guid.NewGuid(), null, null, UserRoleType.SuperAdmin);

        var result = await controller.GetSummary(CancellationToken.None);
        var okResult = result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(StatusCodes.Status200OK);

        var summary = GetData<DashboardSummaryDto>(result);
        summary.Should().NotBeNull();
        summary!.IsSuperAdmin.Should().BeTrue();
        summary.TotalOrganizations.Should().Be(2);
        summary.TotalBranches.Should().Be(2);
        summary.TotalUsers.Should().Be(2);
        summary.TotalStaff.Should().Be(1);
        summary.TotalStudents.Should().Be(1);
        summary.RecentActivities.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetDashboardSummary_AsOrgAdmin_ReturnsTenantScopedAggregates()
    {
        string dbName = $"DashboardOrgAdmin_{Guid.NewGuid()}";
        var org1Id = Guid.NewGuid();
        var org2Id = Guid.NewGuid();
        var branch1Id = Guid.NewGuid();
        var branch2Id = Guid.NewGuid();
        var adminId = Guid.NewGuid();

        var tenantContext = CreateTenantContext(org1Id, branch1Id, false);
        using var context = CreateInMemoryDbContext(dbName, tenantContext);

        var org1 = new Organization { Id = org1Id, Name = "Pak2Source Academy", Code = "P2S-ACADEMY", IsActive = true };
        var org2 = new Organization { Id = org2Id, Name = "Other School", Code = "OTHER", IsActive = true };

        var branch1 = new Branch { Id = branch1Id, OrganizationId = org1Id, Name = "Main Campus", Code = "MAIN", IsActive = true };
        var branch2 = new Branch { Id = branch2Id, OrganizationId = org2Id, Name = "Other Branch", Code = "OTH", IsActive = true };

        var user1 = new User { Id = adminId, OrganizationId = org1Id, BranchId = branch1Id, Username = "orgadmin", Email = "admin@p2s.edu", RoleType = UserRoleType.OrganizationAdmin, IsActive = true };
        var user2 = new User { Id = Guid.NewGuid(), OrganizationId = org1Id, BranchId = branch1Id, Username = "student1", Email = "s1@p2s.edu", RoleType = UserRoleType.Student, IsActive = true };
        var otherUser = new User { Id = Guid.NewGuid(), OrganizationId = org2Id, BranchId = branch2Id, Username = "other1", Email = "other@other.edu", RoleType = UserRoleType.Teacher, IsActive = true };

        var log1 = new AuditLog
        {
            Id = Guid.NewGuid(),
            OrganizationId = org1Id,
            Action = AuditAction.Login,
            Module = "Auth",
            EntityName = "User",
            Details = "User logged in",
            TimestampUtc = DateTime.UtcNow
        };

        var log2 = new AuditLog
        {
            Id = Guid.NewGuid(),
            OrganizationId = org2Id,
            Action = AuditAction.Login,
            Module = "Auth",
            EntityName = "User",
            Details = "Other user logged in",
            TimestampUtc = DateTime.UtcNow
        };

        context.Organizations.AddRange(org1, org2);
        context.Branches.AddRange(branch1, branch2);
        context.Users.AddRange(user1, user2, otherUser);
        context.AuditLogs.AddRange(log1, log2);
        await context.SaveChangesAsync();

        var controller = new DashboardController(context);
        SetupControllerContext(controller, adminId, org1Id, branch1Id, UserRoleType.OrganizationAdmin);

        var result = await controller.GetSummary(CancellationToken.None);
        var okResult = result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(StatusCodes.Status200OK);

        var summary = GetData<DashboardSummaryDto>(result);
        summary.Should().NotBeNull();
        summary!.IsSuperAdmin.Should().BeFalse();
        summary.OrganizationName.Should().Be("Pak2Source Academy");
        summary.BranchName.Should().Be("Main Campus");
        summary.TotalBranches.Should().Be(1);
        summary.TotalUsers.Should().Be(2);
        summary.TotalStaff.Should().Be(0);
        summary.TotalStudents.Should().Be(1);
        summary.RecentActivities.Should().HaveCount(1);
    }
}
