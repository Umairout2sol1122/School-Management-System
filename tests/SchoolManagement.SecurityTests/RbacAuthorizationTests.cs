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

public class RbacAuthorizationTests
{
    private readonly PasswordHasher _passwordHasher = new();
    private readonly Mock<ITokenService> _tokenServiceMock = new();
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
        currentUserServiceMock.Setup(s => s.Email).Returns("test@pak2source.com");

        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock.Setup(sp => sp.GetService(typeof(ICurrentUserService)))
            .Returns(currentUserServiceMock.Object);
        serviceProviderMock.Setup(sp => sp.GetService(typeof(ITenantContext)))
            .Returns(CreateTenantContext(orgId, null, role == UserRoleType.SuperAdmin));

        httpContext.RequestServices = serviceProviderMock.Object;
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
    }

    [Fact]
    public async Task AccountLockout_After5ConsecutiveFailedLogins_ShouldLockAccount()
    {
        string dbName = $"LockoutDb_{Guid.NewGuid()}";
        var tenantContext = CreateTenantContext(null, null, true);
        using var context = CreateInMemoryDbContext(dbName, tenantContext);

        var org = new Organization
        {
            Id = Guid.NewGuid(),
            Name = "Lockout Academy",
            Code = "SCH-LOCK",
            IsActive = true
        };
        context.Organizations.Add(org);

        var (hash, salt) = _passwordHasher.HashPassword("Correct@2026!");
        var user = new User
        {
            Id = Guid.NewGuid(),
            OrganizationId = org.Id,
            Email = "lockout.target@school.edu",
            Username = "lockout_target",
            PasswordHash = hash,
            PasswordSalt = salt,
            IsActive = true,
            AccessFailedCount = 0,
            IsLocked = false
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var authController = new AuthController(context, _passwordHasher, _tokenServiceMock.Object, _auditServiceMock.Object);
        authController.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

        // 5 consecutive wrong password attempts
        for (int i = 1; i <= 5; i++)
        {
            var result = await authController.Login(new LoginRequestDto("lockout.target@school.edu", "WrongPassword!"), CancellationToken.None);
            var statusResult = result as ObjectResult;
            statusResult.Should().NotBeNull();
            statusResult!.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        }

        // Verify account is now locked
        var lockedUser = await context.Users.IgnoreQueryFilters().FirstAsync(u => u.Id == user.Id);
        lockedUser.IsLocked.Should().BeTrue();
        lockedUser.LockoutEndUtc.Should().BeAfter(DateTime.UtcNow);
        lockedUser.AccessFailedCount.Should().Be(5);

        // 6th attempt should return 423 Locked
        var lockedResult = await authController.Login(new LoginRequestDto("lockout.target@school.edu", "WrongPassword!"), CancellationToken.None);
        var lockedStatusResult = lockedResult as ObjectResult;
        lockedStatusResult.Should().NotBeNull();
        lockedStatusResult!.StatusCode.Should().Be(StatusCodes.Status423Locked);
    }

    [Fact]
    public async Task AccountUnlock_AdminCanUnlockLockedAccount()
    {
        string dbName = $"UnlockDb_{Guid.NewGuid()}";
        var orgId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var tenantContext = CreateTenantContext(orgId);
        using var context = CreateInMemoryDbContext(dbName, tenantContext);

        var org = new Organization
        {
            Id = orgId,
            Name = "Unlock School",
            Code = "SCH-UNLOCK",
            IsActive = true
        };
        context.Organizations.Add(org);

        var lockedUser = new User
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            Email = "locked@school.edu",
            Username = "locked_user",
            FirstName = "Locked",
            LastName = "User",
            PasswordHash = "hash",
            PasswordSalt = "salt",
            IsActive = true,
            IsLocked = true,
            LockoutEndUtc = DateTime.UtcNow.AddMinutes(15),
            AccessFailedCount = 5
        };
        context.Users.Add(lockedUser);
        await context.SaveChangesAsync();

        var usersController = new UsersController(context, _passwordHasher, _auditServiceMock.Object);
        SetupControllerContext(usersController, adminId, orgId, UserRoleType.OrganizationAdmin);

        var actionResult = await usersController.UnlockAccount(lockedUser.Id, CancellationToken.None);
        var okResult = actionResult as OkObjectResult;
        okResult.Should().NotBeNull();

        var refreshedUser = await context.Users.FirstAsync(u => u.Id == lockedUser.Id);
        refreshedUser.IsLocked.Should().BeFalse();
        refreshedUser.LockoutEndUtc.Should().BeNull();
        refreshedUser.AccessFailedCount.Should().Be(0);
    }

    [Fact]
    public async Task PasswordChange_WithValidCurrentPassword_ShouldUpdateHashAndRevokeTokens()
    {
        string dbName = $"PwdChangeDb_{Guid.NewGuid()}";
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var tenantContext = CreateTenantContext(orgId);
        using var context = CreateInMemoryDbContext(dbName, tenantContext);

        var org = new Organization
        {
            Id = orgId,
            Name = "Password Change School",
            Code = "SCH-PWD",
            IsActive = true
        };
        context.Organizations.Add(org);

        var (hash, salt) = _passwordHasher.HashPassword("OldPass@2026!");
        var user = new User
        {
            Id = userId,
            OrganizationId = orgId,
            Email = "change.pwd@school.edu",
            Username = "changepwd",
            PasswordHash = hash,
            PasswordSalt = salt,
            IsActive = true
        };

        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Token = "active_refresh_token",
            ExpiresAtUtc = DateTime.UtcNow.AddDays(7),
            RevokedAtUtc = null
        };

        context.Users.Add(user);
        context.RefreshTokens.Add(refreshToken);
        await context.SaveChangesAsync();

        var authController = new AuthController(context, _passwordHasher, _tokenServiceMock.Object, _auditServiceMock.Object);
        SetupControllerContext(authController, userId, orgId, UserRoleType.Teacher);

        var request = new ChangePasswordRequestDto("OldPass@2026!", "NewStrongPass@2026!", "NewStrongPass@2026!");
        var result = await authController.ChangePassword(request, CancellationToken.None);

        var okResult = result as OkObjectResult;
        okResult.Should().NotBeNull();

        // Verify password changed
        var updatedUser = await context.Users.FirstAsync(u => u.Id == userId);
        _passwordHasher.VerifyPassword("NewStrongPass@2026!", updatedUser.PasswordHash, updatedUser.PasswordSalt).Should().BeTrue();
        _passwordHasher.VerifyPassword("OldPass@2026!", updatedUser.PasswordHash, updatedUser.PasswordSalt).Should().BeFalse();

        // Verify refresh token revoked
        var token = await context.RefreshTokens.FirstAsync(t => t.Id == refreshToken.Id);
        token.RevokedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task CrossTenant_OrgAdminCannotViewUserFromAnotherOrganization()
    {
        string dbName = $"CrossTenantDb_{Guid.NewGuid()}";
        var orgAId = Guid.NewGuid();
        var orgBId = Guid.NewGuid();
        var adminAId = Guid.NewGuid();
        var userBId = Guid.NewGuid();

        var tenantContext = CreateTenantContext(orgAId, null, false);
        using var context = CreateInMemoryDbContext(dbName, tenantContext);

        var userB = new User
        {
            Id = userBId,
            OrganizationId = orgBId,
            Email = "userb@beta.edu",
            Username = "user_b",
            FirstName = "User",
            LastName = "Beta",
            PasswordHash = "h",
            PasswordSalt = "s",
            RoleType = UserRoleType.Teacher,
            IsActive = true
        };

        // Seed with disabled filter
        using (var seedContext = CreateInMemoryDbContext(dbName, CreateTenantContext(null, null, true)))
        {
            seedContext.Users.Add(userB);
            await seedContext.SaveChangesAsync();
        }

        var usersController = new UsersController(context, _passwordHasher, _auditServiceMock.Object);
        SetupControllerContext(usersController, adminAId, orgAId, UserRoleType.OrganizationAdmin);

        var result = await usersController.GetUserById(userBId, CancellationToken.None);

        // With global query filter active, entity is filtered out (404) or blocked (403)
        var objResult = result as ObjectResult;
        objResult.Should().NotBeNull();
        objResult!.StatusCode.Should().Match(s => s == StatusCodes.Status404NotFound || s == StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task NonSuperAdmin_CannotCreateSuperAdminUser()
    {
        string dbName = $"SuperAdminEscalationDb_{Guid.NewGuid()}";
        var orgId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var tenantContext = CreateTenantContext(orgId);
        using var context = CreateInMemoryDbContext(dbName, tenantContext);

        var usersController = new UsersController(context, _passwordHasher, _auditServiceMock.Object);
        SetupControllerContext(usersController, adminId, orgId, UserRoleType.OrganizationAdmin);

        var request = new CreateUserRequestDto(
            orgId,
            null,
            "rogue_superadmin",
            "rogue@superadmin.com",
            "Rogue",
            "Admin",
            "StrongPass@2026!",
            UserRoleType.SuperAdmin, // Escalation attempt
            null,
            null
        );

        var result = await usersController.CreateUser(request, CancellationToken.None);

        var objResult = result as ObjectResult;
        objResult.Should().NotBeNull();
        objResult!.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }
}
