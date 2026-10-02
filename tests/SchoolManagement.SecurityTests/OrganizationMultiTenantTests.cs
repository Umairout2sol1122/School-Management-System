using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using SchoolManagement.Api.Controllers;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.Common.Models;
using SchoolManagement.Domain.Entities.Tenant;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.Infrastructure.Security;
using Xunit;

namespace SchoolManagement.SecurityTests;

public class OrganizationMultiTenantTests
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
            claims.Add(new Claim("organization_id", orgId.Value.ToString()));
        claims.Add(new Claim(ClaimTypes.Role, role.ToString()));

        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext
        {
            User = principal
        };

        var currentUserServiceMock = new Mock<ICurrentUserService>();
        currentUserServiceMock.Setup(s => s.UserId).Returns(userId);
        currentUserServiceMock.Setup(s => s.OrganizationId).Returns(orgId);
        currentUserServiceMock.Setup(s => s.IsSuperAdmin).Returns(role == UserRoleType.SuperAdmin);
        currentUserServiceMock.Setup(s => s.Email).Returns("superadmin@pak2source.com");

        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock.Setup(sp => sp.GetService(typeof(ICurrentUserService)))
            .Returns(currentUserServiceMock.Object);
        serviceProviderMock.Setup(sp => sp.GetService(typeof(ITenantContext)))
            .Returns(CreateTenantContext(orgId, null, role == UserRoleType.SuperAdmin));

        httpContext.RequestServices = serviceProviderMock.Object;
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
    }

    [Fact]
    public async Task SuperAdmin_CanCreateOrganizationWithMainBranchAndAdmin()
    {
        string dbName = $"CreateOrgDb_{Guid.NewGuid()}";
        var superAdminId = Guid.NewGuid();
        var tenantContext = CreateTenantContext(null, null, true);
        using var context = CreateInMemoryDbContext(dbName, tenantContext);

        var controller = new OrganizationsController(context, _passwordHasher, _auditServiceMock.Object);
        SetupControllerContext(controller, superAdminId, null, UserRoleType.SuperAdmin);

        var request = new CreateOrganizationRequestDto(
            "Crescent International School",
            "CRESCENT-01",
            "info@crescent.edu",
            "+92-300-9876543",
            "Plot 42, Education City",
            null,
            "#6C5CE7",
            "#5840BA",
            "admin@crescent.edu",
            "crescent_admin",
            "CrescentAdmin@2026!",
            "Crescent",
            "Principal"
        );

        var result = await controller.CreateOrganization(request, CancellationToken.None);
        var okResult = result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(StatusCodes.Status200OK);

        // Verify organization created
        var org = await context.Organizations.Include(o => o.Branches).FirstAsync(o => o.Code == "CRESCENT-01");
        org.Name.Should().Be("Crescent International School");
        org.IsActive.Should().BeTrue();
        org.Branches.Should().HaveCount(1);

        // Verify main branch
        var mainBranch = org.Branches.First();
        mainBranch.IsMainBranch.Should().BeTrue();
        mainBranch.Name.Should().Contain("Main Campus");

        // Verify initial admin user
        var adminUser = await context.Users.FirstAsync(u => u.Email == "admin@crescent.edu");
        adminUser.OrganizationId.Should().Be(org.Id);
        adminUser.BranchId.Should().Be(mainBranch.Id);
        adminUser.RoleType.Should().Be(UserRoleType.OrganizationAdmin);
        _passwordHasher.VerifyPassword("CrescentAdmin@2026!", adminUser.PasswordHash, adminUser.PasswordSalt).Should().BeTrue();
    }

    [Fact]
    public async Task NonSuperAdmin_CannotCreateOrganization()
    {
        string dbName = $"NonSuperAdminCreateOrgDb_{Guid.NewGuid()}";
        var orgAdminId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var tenantContext = CreateTenantContext(orgId);
        using var context = CreateInMemoryDbContext(dbName, tenantContext);

        var controller = new OrganizationsController(context, _passwordHasher, _auditServiceMock.Object);
        SetupControllerContext(controller, orgAdminId, orgId, UserRoleType.OrganizationAdmin);

        var request = new CreateOrganizationRequestDto(
            "Illegal School",
            "ILLEGAL",
            "info@illegal.edu",
            "+92-300-0000000",
            "Address",
            null,
            null,
            null,
            "admin@illegal.edu",
            "illegal_admin",
            "IllegalAdmin@2026!",
            "First",
            "Last"
        );

        var result = await controller.CreateOrganization(request, CancellationToken.None);
        var objResult = result as ObjectResult;
        objResult.Should().NotBeNull();
        objResult!.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task DuplicateOrganizationCode_ShouldBeRejected()
    {
        string dbName = $"DupOrgDb_{Guid.NewGuid()}";
        var superAdminId = Guid.NewGuid();
        var tenantContext = CreateTenantContext(null, null, true);
        using var context = CreateInMemoryDbContext(dbName, tenantContext);

        context.Organizations.Add(new Organization
        {
            Id = Guid.NewGuid(),
            Name = "Existing School",
            Code = "DUP-CODE",
            IsActive = true
        });
        await context.SaveChangesAsync();

        var controller = new OrganizationsController(context, _passwordHasher, _auditServiceMock.Object);
        SetupControllerContext(controller, superAdminId, null, UserRoleType.SuperAdmin);

        var request = new CreateOrganizationRequestDto(
            "Another School",
            "dup-code", // Lowercase duplicate
            "another@school.edu",
            "+92-300-1111111",
            "Address",
            null,
            null,
            null,
            "admin@another.edu",
            "another_admin",
            "AnotherAdmin@2026!",
            "First",
            "Last"
        );

        var result = await controller.CreateOrganization(request, CancellationToken.None);
        var objResult = result as ObjectResult;
        objResult.Should().NotBeNull();
        objResult!.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task Branch_CannotDeleteMainBranch()
    {
        string dbName = $"DeleteMainBranchDb_{Guid.NewGuid()}";
        var orgId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var tenantContext = CreateTenantContext(orgId);
        using var context = CreateInMemoryDbContext(dbName, tenantContext);

        var org = new Organization { Id = orgId, Name = "Org", Code = "ORG-01", IsActive = true };
        var mainBranch = new Branch
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            Name = "Main Campus",
            Code = "ORG-MAIN",
            IsMainBranch = true,
            IsActive = true
        };
        context.Organizations.Add(org);
        context.Branches.Add(mainBranch);
        await context.SaveChangesAsync();

        var controller = new BranchesController(context, _auditServiceMock.Object);
        SetupControllerContext(controller, adminId, orgId, UserRoleType.OrganizationAdmin);

        var result = await controller.DeleteBranch(mainBranch.Id, CancellationToken.None);
        var objResult = result as ObjectResult;
        objResult.Should().NotBeNull();
        objResult!.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task Branch_DuplicateBranchCodeInSameOrg_ShouldBeRejected()
    {
        string dbName = $"DupBranchDb_{Guid.NewGuid()}";
        var orgId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var tenantContext = CreateTenantContext(orgId);
        using var context = CreateInMemoryDbContext(dbName, tenantContext);

        var org = new Organization { Id = orgId, Name = "Org", Code = "ORG-02", IsActive = true };
        var existingBranch = new Branch
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            Name = "City Campus",
            Code = "CAMPUS-CITY",
            IsActive = true
        };
        context.Organizations.Add(org);
        context.Branches.Add(existingBranch);
        await context.SaveChangesAsync();

        var controller = new BranchesController(context, _auditServiceMock.Object);
        SetupControllerContext(controller, adminId, orgId, UserRoleType.OrganizationAdmin);

        var request = new CreateBranchRequestDto(
            orgId,
            "City Campus 2",
            "campus-city", // Duplicate code
            "City Road",
            "+92-300-2222222",
            "city@org.edu"
        );

        var result = await controller.CreateBranch(request, CancellationToken.None);
        var objResult = result as ObjectResult;
        objResult.Should().NotBeNull();
        objResult!.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }
}
