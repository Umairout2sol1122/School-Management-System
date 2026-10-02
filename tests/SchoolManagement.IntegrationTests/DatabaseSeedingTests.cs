using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Common.Models;
using SchoolManagement.Domain.Constants;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.Infrastructure.Security;
using Xunit;

namespace SchoolManagement.IntegrationTests;

public class DatabaseSeedingTests
{
    private ApplicationDbContext CreateContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        var tenantContext = new TenantContext();
        tenantContext.SetTenant(null, null, isSuperAdmin: true);

        return new ApplicationDbContext(options, tenantContext);
    }

    [Fact]
    public async Task SeedInitialDataAsync_ShouldPopulatePermissionsAndSuperAdmin()
    {
        // Arrange
        string dbName = $"SeedDb_{Guid.NewGuid()}";
        using var context = CreateContext(dbName);
        var hasher = new PasswordHasher();

        // Act
        await DatabaseSeeder.SeedInitialDataAsync(context, hasher);

        // Assert - Permissions
        var permissions = await context.Permissions.ToListAsync();
        permissions.Should().HaveCount(Permissions.All.Count);
        permissions.Select(p => p.Code).Should().Contain(Permissions.OrganizationsManage);
        permissions.Select(p => p.Code).Should().Contain(Permissions.StudentsCreate);
        permissions.Select(p => p.Code).Should().Contain(Permissions.FeesCollectPayment);

        // Assert - Roles
        var superAdminRole = await context.Roles.FirstOrDefaultAsync(r => r.Name == nameof(UserRoleType.SuperAdmin));
        superAdminRole.Should().NotBeNull();
        superAdminRole!.IsSystemRole.Should().BeTrue();

        var orgAdminRole = await context.Roles.FirstOrDefaultAsync(r => r.Name == nameof(UserRoleType.OrganizationAdmin));
        orgAdminRole.Should().NotBeNull();

        // Assert - RolePermissions for SuperAdmin
        var superAdminRolePerms = await context.RolePermissions
            .Where(rp => rp.RoleId == superAdminRole.Id)
            .ToListAsync();
        superAdminRolePerms.Should().HaveCount(Permissions.All.Count);

        // Assert - SuperAdmin User
        var superAdminUser = await context.Users
            .FirstOrDefaultAsync(u => u.RoleType == UserRoleType.SuperAdmin);
        superAdminUser.Should().NotBeNull();
        superAdminUser!.Username.Should().Be("superadmin");
        superAdminUser.Email.Should().Be("superadmin@pak2source.com");
        hasher.VerifyPassword("SuperAdmin@2026!", superAdminUser.PasswordHash, superAdminUser.PasswordSalt).Should().BeTrue();

        // Assert - Demo School Organization
        var demoOrg = await context.Organizations
            .FirstOrDefaultAsync(o => o.Code == "P2S-ACADEMY");
        demoOrg.Should().NotBeNull();
        demoOrg!.ThemePrimaryColor.Should().Be("#924EC0");
        demoOrg.ThemeSecondaryColor.Should().Be("#5840BA");

        var mainBranch = await context.Branches
            .FirstOrDefaultAsync(b => b.OrganizationId == demoOrg.Id);
        mainBranch.Should().NotBeNull();
        mainBranch!.IsMainBranch.Should().BeTrue();
    }

    [Fact]
    public async Task SeedInitialDataAsync_CalledTwice_ShouldBeIdempotentWithoutDuplicates()
    {
        // Arrange
        string dbName = $"SeedDb_{Guid.NewGuid()}";
        using var context = CreateContext(dbName);
        var hasher = new PasswordHasher();

        // Act - Call twice
        await DatabaseSeeder.SeedInitialDataAsync(context, hasher);
        await DatabaseSeeder.SeedInitialDataAsync(context, hasher);

        // Assert
        int permissionCount = await context.Permissions.CountAsync();
        permissionCount.Should().Be(Permissions.All.Count);

        int superAdminCount = await context.Users.CountAsync(u => u.RoleType == UserRoleType.SuperAdmin);
        superAdminCount.Should().Be(1);

        int demoOrgCount = await context.Organizations.CountAsync(o => o.Code == "P2S-ACADEMY");
        demoOrgCount.Should().Be(1);
    }
}
