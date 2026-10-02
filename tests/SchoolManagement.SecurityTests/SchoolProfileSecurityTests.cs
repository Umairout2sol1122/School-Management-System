using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using SchoolManagement.Api.Controllers;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.Common.Models;
using SchoolManagement.Domain.Entities.Security;
using SchoolManagement.Domain.Entities.Tenant;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.Infrastructure.Security;
using Xunit;

namespace SchoolManagement.SecurityTests;

public class SchoolProfileSecurityTests
{
    private readonly PasswordHasher _passwordHasher = new();
    private readonly Mock<IAuditService> _auditServiceMock = new();

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

    private void SetupControllerContext(BaseApiController controller, Guid? userId, Guid? orgId, UserRoleType role)
    {
        var claims = new List<Claim>();
        if (userId.HasValue)
            claims.Add(new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()));
        if (orgId.HasValue)
            claims.Add(new Claim("tenant_id", orgId.Value.ToString()));
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
        currentUserServiceMock.Setup(s => s.RoleType).Returns(role);
        currentUserServiceMock.Setup(s => s.IsSuperAdmin).Returns(role == UserRoleType.SuperAdmin);
        currentUserServiceMock.Setup(s => s.Email).Returns("admin@p2s-academy.edu");

        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock.Setup(sp => sp.GetService(typeof(ICurrentUserService)))
            .Returns(currentUserServiceMock.Object);
        serviceProviderMock.Setup(sp => sp.GetService(typeof(ITenantContext)))
            .Returns(CreateTenantContext(orgId, null, role == UserRoleType.SuperAdmin));

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
    public async Task GetMySchool_AsOrgAdmin_ReturnsSchoolProfileWithCounts()
    {
        string dbName = $"GetMySchool_{Guid.NewGuid()}";
        var orgId = Guid.NewGuid();
        var adminId = Guid.NewGuid();

        var tenantContext = CreateTenantContext(orgId, null, false);
        using var context = CreateInMemoryDbContext(dbName, tenantContext);

        var org = new Organization
        {
            Id = orgId,
            Name = "Pak2Source Academy",
            Code = "P2S-ACADEMY",
            Email = "info@p2s-academy.edu",
            Phone = "+92-300-1234567",
            Address = "Plot 10, Sector G-10",
            ThemePrimaryColor = "#6C5CE7",
            ThemeSecondaryColor = "#5840BA",
            ThemeDarkMode = true,
            IsActive = true
        };

        var branch = new Branch
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            Name = "Main Campus",
            Code = "P2S-MAIN",
            IsActive = true
        };

        var user = new User
        {
            Id = adminId,
            OrganizationId = orgId,
            Username = "orgadmin",
            Email = "admin@p2s-academy.edu",
            RoleType = UserRoleType.OrganizationAdmin,
            IsActive = true
        };

        context.Organizations.Add(org);
        context.Branches.Add(branch);
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var controller = new OrganizationsController(context, _passwordHasher, _auditServiceMock.Object);
        SetupControllerContext(controller, adminId, orgId, UserRoleType.OrganizationAdmin);

        var result = await controller.GetMySchool(CancellationToken.None);
        var okResult = result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(StatusCodes.Status200OK);

        var profile = GetData<SchoolProfileDto>(result);
        profile.Should().NotBeNull();
        profile!.Name.Should().Be("Pak2Source Academy");
        profile.Code.Should().Be("P2S-ACADEMY");
        profile.BranchesCount.Should().Be(1);
        profile.UsersCount.Should().Be(1);
        profile.ThemePrimaryColor.Should().Be("#6C5CE7");
    }

    [Fact]
    public async Task UpdateMySchool_AsOrgAdmin_UpdatesBrandingAndProfile()
    {
        string dbName = $"UpdateMySchool_{Guid.NewGuid()}";
        var orgId = Guid.NewGuid();
        var adminId = Guid.NewGuid();

        var tenantContext = CreateTenantContext(orgId, null, false);
        using var context = CreateInMemoryDbContext(dbName, tenantContext);

        var org = new Organization
        {
            Id = orgId,
            Name = "Pak2Source Academy",
            Code = "P2S-ACADEMY",
            Email = "info@p2s-academy.edu",
            ThemePrimaryColor = "#6C5CE7",
            ThemeSecondaryColor = "#5840BA",
            ThemeDarkMode = true,
            IsActive = true
        };

        context.Organizations.Add(org);
        await context.SaveChangesAsync();

        var controller = new OrganizationsController(context, _passwordHasher, _auditServiceMock.Object);
        SetupControllerContext(controller, adminId, orgId, UserRoleType.OrganizationAdmin);

        var updateRequest = new UpdateSchoolProfileRequestDto(
            "Pak2Source Elite Grammar School",
            "contact@p2s-elite.edu",
            "+92-42-35889900",
            "Gulberg III, Lahore",
            "https://cdn.p2s.edu/logos/elite.png",
            "#00B894", // Mint accent
            "#00A884",
            true
        );

        var result = await controller.UpdateMySchool(updateRequest, CancellationToken.None);
        var okResult = result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(StatusCodes.Status200OK);

        // Verify database persistence
        var updatedOrg = await context.Organizations.FirstAsync(o => o.Id == orgId);
        updatedOrg.Name.Should().Be("Pak2Source Elite Grammar School");
        updatedOrg.Email.Should().Be("contact@p2s-elite.edu");
        updatedOrg.Phone.Should().Be("+92-42-35889900");
        updatedOrg.Address.Should().Be("Gulberg III, Lahore");
        updatedOrg.ThemePrimaryColor.Should().Be("#00B894");
        updatedOrg.Code.Should().Be("P2S-ACADEMY"); // Code remains unchanged
    }

    [Fact]
    public async Task UpdateMySchool_AsStaff_ReturnsForbidden()
    {
        string dbName = $"UpdateMySchoolStaff_{Guid.NewGuid()}";
        var orgId = Guid.NewGuid();
        var staffId = Guid.NewGuid();

        var tenantContext = CreateTenantContext(orgId, null, false);
        using var context = CreateInMemoryDbContext(dbName, tenantContext);

        var org = new Organization
        {
            Id = orgId,
            Name = "Pak2Source Academy",
            Code = "P2S-ACADEMY",
            IsActive = true
        };

        context.Organizations.Add(org);
        await context.SaveChangesAsync();

        var controller = new OrganizationsController(context, _passwordHasher, _auditServiceMock.Object);
        SetupControllerContext(controller, staffId, orgId, UserRoleType.Staff); // Staff role, not OrgAdmin

        var updateRequest = new UpdateSchoolProfileRequestDto(
            "Hacked Name",
            null,
            null,
            null,
            null,
            null,
            null,
            true
        );

        var result = await controller.UpdateMySchool(updateRequest, CancellationToken.None);
        var objectResult = result as ObjectResult;
        objectResult.Should().NotBeNull();
        objectResult!.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task GetMySchool_WithoutTenantContext_ReturnsBadRequest()
    {
        string dbName = $"GetMySchoolNoTenant_{Guid.NewGuid()}";
        var tenantContext = CreateTenantContext(null, null, false);
        using var context = CreateInMemoryDbContext(dbName, tenantContext);

        var controller = new OrganizationsController(context, _passwordHasher, _auditServiceMock.Object);
        SetupControllerContext(controller, Guid.NewGuid(), null, UserRoleType.Staff);

        var result = await controller.GetMySchool(CancellationToken.None);
        var objectResult = result as ObjectResult;
        objectResult.Should().NotBeNull();
        objectResult!.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }
}
