using System.IdentityModel.Tokens.Jwt;
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
using SchoolManagement.Infrastructure.Configuration;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.Infrastructure.Security;
using Xunit;

namespace SchoolManagement.SecurityTests;

public class BranchSwitchingSecurityTests
{
    private readonly PasswordHasher _passwordHasher = new();
    private readonly Mock<IAuditService> _auditServiceMock = new();
    private readonly TokenService _tokenService;

    public BranchSwitchingSecurityTests()
    {
        var jwtSettings = new JwtSettings
        {
            Secret = "SchoolManagementMasterSecretKey2026!MustBeVeryLongForHmacSha256",
            Issuer = "SchoolManagementApi",
            Audience = "SchoolManagementClient",
            ExpiryMinutes = 60,
            RefreshTokenExpiryDays = 7
        };
        _tokenService = new TokenService(jwtSettings);
    }

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
        httpContext.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("127.0.0.1");

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
    public async Task SwitchBranch_WithValidBranchInSameOrg_ReturnsNewTokenWithUpdatedBranch()
    {
        string dbName = $"SwitchBranchValid_{Guid.NewGuid()}";
        var orgId = Guid.NewGuid();
        var branchAId = Guid.NewGuid();
        var branchBId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var tenantContext = CreateTenantContext(orgId, branchAId, false);
        using var context = CreateInMemoryDbContext(dbName, tenantContext);

        var org = new Organization
        {
            Id = orgId,
            Name = "Pak2Source Academy",
            Code = "P2S-ACADEMY",
            IsActive = true
        };

        var branchA = new Branch
        {
            Id = branchAId,
            OrganizationId = orgId,
            Name = "Main Campus",
            Code = "MAIN",
            IsActive = true
        };

        var branchB = new Branch
        {
            Id = branchBId,
            OrganizationId = orgId,
            Name = "North Campus",
            Code = "NORTH",
            IsActive = true
        };

        var (hash, salt) = _passwordHasher.HashPassword("OrgAdmin@2026!");
        var user = new User
        {
            Id = userId,
            OrganizationId = orgId,
            BranchId = branchAId,
            Username = "orgadmin",
            Email = "admin@p2s-academy.edu",
            PasswordHash = hash,
            PasswordSalt = salt,
            RoleType = UserRoleType.OrganizationAdmin,
            IsActive = true
        };

        context.Organizations.Add(org);
        context.Branches.AddRange(branchA, branchB);
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var authController = new AuthController(context, _passwordHasher, _tokenService, _auditServiceMock.Object);
        SetupControllerContext(authController, userId, orgId, branchAId, UserRoleType.OrganizationAdmin);

        var result = await authController.SwitchBranch(branchBId, CancellationToken.None);
        var okResult = result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(StatusCodes.Status200OK);

        // Verify response payload
        var dataProp = GetData<AuthResultDto>(result);
        dataProp.Should().NotBeNull();
        dataProp!.Success.Should().BeTrue();
        dataProp.User.Should().NotBeNull();
        dataProp.User!.BranchId.Should().Be(branchBId);
        dataProp.User.BranchName.Should().Be("North Campus");

        // Verify JWT token has new branch_id claim
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(dataProp.AccessToken);
        var branchClaim = jwt.Claims.FirstOrDefault(c => c.Type == "branch_id")?.Value;
        branchClaim.Should().Be(branchBId.ToString());

        // Verify database user.BranchId updated
        var updatedUser = await context.Users.IgnoreQueryFilters().FirstAsync(u => u.Id == userId);
        updatedUser.BranchId.Should().Be(branchBId);
    }

    [Fact]
    public async Task SwitchBranch_CrossTenantBranch_ReturnsForbidden()
    {
        string dbName = $"SwitchBranchCrossTenant_{Guid.NewGuid()}";
        var org1Id = Guid.NewGuid();
        var org2Id = Guid.NewGuid();
        var branch1Id = Guid.NewGuid();
        var branch2Id = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var tenantContext = CreateTenantContext(org1Id, branch1Id, false);
        using var context = CreateInMemoryDbContext(dbName, tenantContext);

        var org1 = new Organization { Id = org1Id, Name = "Org 1", Code = "ORG-1", IsActive = true };
        var org2 = new Organization { Id = org2Id, Name = "Org 2", Code = "ORG-2", IsActive = true };

        var branch1 = new Branch { Id = branch1Id, OrganizationId = org1Id, Name = "Branch 1", Code = "B1", IsActive = true };
        var branch2 = new Branch { Id = branch2Id, OrganizationId = org2Id, Name = "Branch 2", Code = "B2", IsActive = true };

        var (hash, salt) = _passwordHasher.HashPassword("OrgAdmin@2026!");
        var user = new User
        {
            Id = userId,
            OrganizationId = org1Id,
            BranchId = branch1Id,
            Username = "org1admin",
            Email = "admin@org1.edu",
            PasswordHash = hash,
            PasswordSalt = salt,
            RoleType = UserRoleType.OrganizationAdmin,
            IsActive = true
        };

        context.Organizations.AddRange(org1, org2);
        context.Branches.AddRange(branch1, branch2);
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var authController = new AuthController(context, _passwordHasher, _tokenService, _auditServiceMock.Object);
        SetupControllerContext(authController, userId, org1Id, branch1Id, UserRoleType.OrganizationAdmin);

        var result = await authController.SwitchBranch(branch2Id, CancellationToken.None);
        var objectResult = result as ObjectResult;
        objectResult.Should().NotBeNull();
        objectResult!.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task SwitchBranch_InactiveBranch_ReturnsBadRequest()
    {
        string dbName = $"SwitchBranchInactive_{Guid.NewGuid()}";
        var orgId = Guid.NewGuid();
        var branch1Id = Guid.NewGuid();
        var inactiveBranchId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var tenantContext = CreateTenantContext(orgId, branch1Id, false);
        using var context = CreateInMemoryDbContext(dbName, tenantContext);

        var org = new Organization { Id = orgId, Name = "Org 1", Code = "ORG-1", IsActive = true };
        var branch1 = new Branch { Id = branch1Id, OrganizationId = orgId, Name = "Active Branch", Code = "B1", IsActive = true };
        var inactiveBranch = new Branch { Id = inactiveBranchId, OrganizationId = orgId, Name = "Closed Branch", Code = "CLOSED", IsActive = false };

        var (hash, salt) = _passwordHasher.HashPassword("OrgAdmin@2026!");
        var user = new User
        {
            Id = userId,
            OrganizationId = orgId,
            BranchId = branch1Id,
            Username = "orgadmin",
            Email = "admin@org1.edu",
            PasswordHash = hash,
            PasswordSalt = salt,
            RoleType = UserRoleType.OrganizationAdmin,
            IsActive = true
        };

        context.Organizations.Add(org);
        context.Branches.AddRange(branch1, inactiveBranch);
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var authController = new AuthController(context, _passwordHasher, _tokenService, _auditServiceMock.Object);
        SetupControllerContext(authController, userId, orgId, branch1Id, UserRoleType.OrganizationAdmin);

        var result = await authController.SwitchBranch(inactiveBranchId, CancellationToken.None);
        var objectResult = result as ObjectResult;
        objectResult.Should().NotBeNull();
        objectResult!.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task SwitchBranch_NonExistentBranch_ReturnsNotFound()
    {
        string dbName = $"SwitchBranchNotFound_{Guid.NewGuid()}";
        var orgId = Guid.NewGuid();
        var branch1Id = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var tenantContext = CreateTenantContext(orgId, branch1Id, false);
        using var context = CreateInMemoryDbContext(dbName, tenantContext);

        var org = new Organization { Id = orgId, Name = "Org 1", Code = "ORG-1", IsActive = true };
        var branch1 = new Branch { Id = branch1Id, OrganizationId = orgId, Name = "Active Branch", Code = "B1", IsActive = true };

        var (hash, salt) = _passwordHasher.HashPassword("OrgAdmin@2026!");
        var user = new User
        {
            Id = userId,
            OrganizationId = orgId,
            BranchId = branch1Id,
            Username = "orgadmin",
            Email = "admin@org1.edu",
            PasswordHash = hash,
            PasswordSalt = salt,
            RoleType = UserRoleType.OrganizationAdmin,
            IsActive = true
        };

        context.Organizations.Add(org);
        context.Branches.Add(branch1);
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var authController = new AuthController(context, _passwordHasher, _tokenService, _auditServiceMock.Object);
        SetupControllerContext(authController, userId, orgId, branch1Id, UserRoleType.OrganizationAdmin);

        var result = await authController.SwitchBranch(Guid.NewGuid(), CancellationToken.None);
        var objectResult = result as ObjectResult;
        objectResult.Should().NotBeNull();
        objectResult!.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task SwitchBranch_SuperAdminCanSwitchToAnyBranchAcrossOrganizations()
    {
        string dbName = $"SwitchBranchSuperAdmin_{Guid.NewGuid()}";
        var orgId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var superAdminId = Guid.NewGuid();

        var tenantContext = CreateTenantContext(null, null, true);
        using var context = CreateInMemoryDbContext(dbName, tenantContext);

        var org = new Organization { Id = orgId, Name = "Target Org", Code = "TARGET-ORG", IsActive = true };
        var branch = new Branch { Id = branchId, OrganizationId = orgId, Name = "Target Branch", Code = "TB-1", IsActive = true };

        var (hash, salt) = _passwordHasher.HashPassword("SuperAdmin@2026!");
        var superAdmin = new User
        {
            Id = superAdminId,
            OrganizationId = Guid.Empty,
            Username = "superadmin",
            Email = "superadmin@pak2source.com",
            PasswordHash = hash,
            PasswordSalt = salt,
            RoleType = UserRoleType.SuperAdmin,
            IsActive = true
        };

        context.Organizations.Add(org);
        context.Branches.Add(branch);
        context.Users.Add(superAdmin);
        await context.SaveChangesAsync();

        var authController = new AuthController(context, _passwordHasher, _tokenService, _auditServiceMock.Object);
        SetupControllerContext(authController, superAdminId, null, null, UserRoleType.SuperAdmin);

        var result = await authController.SwitchBranch(branchId, CancellationToken.None);
        var okResult = result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(StatusCodes.Status200OK);

        var dataProp = GetData<AuthResultDto>(result);
        dataProp.Should().NotBeNull();
        dataProp!.User!.BranchId.Should().Be(branchId);
    }

    [Fact]
    public async Task CreateUser_WithCrossTenantBranchId_FailsValidation()
    {
        string dbName = $"CreateUserCrossTenantBranch_{Guid.NewGuid()}";
        var org1Id = Guid.NewGuid();
        var org2Id = Guid.NewGuid();
        var branch2Id = Guid.NewGuid();
        var adminId = Guid.NewGuid();

        var tenantContext = CreateTenantContext(org1Id, null, false);
        using var context = CreateInMemoryDbContext(dbName, tenantContext);

        var org1 = new Organization { Id = org1Id, Name = "Org 1", Code = "ORG-1", IsActive = true };
        var org2 = new Organization { Id = org2Id, Name = "Org 2", Code = "ORG-2", IsActive = true };
        var branch2 = new Branch { Id = branch2Id, OrganizationId = org2Id, Name = "Org 2 Branch", Code = "O2-B1", IsActive = true };

        context.Organizations.AddRange(org1, org2);
        context.Branches.Add(branch2);
        await context.SaveChangesAsync();

        var usersController = new UsersController(context, _passwordHasher, _auditServiceMock.Object);
        SetupControllerContext(usersController, adminId, org1Id, null, UserRoleType.OrganizationAdmin);

        var request = new CreateUserRequestDto(
            org1Id,
            branch2Id, // Cross-tenant branch!
            "newteacher",
            "teacher@org1.edu",
            "Jane",
            "Doe",
            "Teacher@2026!",
            UserRoleType.Teacher,
            null,
            null
        );

        var result = await usersController.CreateUser(request, CancellationToken.None);
        var objectResult = result as ObjectResult;
        objectResult.Should().NotBeNull();
        objectResult!.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }
}
