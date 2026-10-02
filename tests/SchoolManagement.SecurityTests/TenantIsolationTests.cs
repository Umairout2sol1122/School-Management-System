using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Common.Models;
using SchoolManagement.Domain.Entities.Security;
using SchoolManagement.Domain.Entities.Tenant;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Infrastructure.Persistence;
using Xunit;

namespace SchoolManagement.SecurityTests;

public class TenantIsolationTests
{
    private readonly Guid _orgAId = Guid.NewGuid();
    private readonly Guid _orgBId = Guid.NewGuid();
    private readonly string _databaseName = $"TenantDb_{Guid.NewGuid()}";

    private async Task SeedDataAsync(ApplicationDbContext context)
    {
        var orgA = new Organization { Id = _orgAId, Name = "Alpha Grammar School", Code = "SCH-ALPHA" };
        var orgB = new Organization { Id = _orgBId, Name = "Beta International School", Code = "SCH-BETA" };

        var userA = new User
        {
            Id = Guid.NewGuid(),
            OrganizationId = _orgAId,
            Username = "alpha_teacher",
            Email = "teacher@alpha.edu",
            PasswordHash = "hashA",
            PasswordSalt = "saltA",
            RoleType = UserRoleType.Teacher
        };

        var userB = new User
        {
            Id = Guid.NewGuid(),
            OrganizationId = _orgBId,
            Username = "beta_teacher",
            Email = "teacher@beta.edu",
            PasswordHash = "hashB",
            PasswordSalt = "saltB",
            RoleType = UserRoleType.Teacher
        };

        var deletedUserA = new User
        {
            Id = Guid.NewGuid(),
            OrganizationId = _orgAId,
            Username = "deleted_teacher",
            Email = "deleted@alpha.edu",
            PasswordHash = "hashD",
            PasswordSalt = "saltD",
            RoleType = UserRoleType.Teacher,
            IsDeleted = true
        };

        context.Organizations.AddRange(orgA, orgB);
        context.Users.AddRange(userA, userB, deletedUserA);
        await context.SaveChangesAsync();
    }

    private ApplicationDbContext CreateContext(Guid? orgId, bool isSuperAdmin)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: _databaseName)
            .Options;

        var tenantContext = new TenantContext();
        tenantContext.SetTenant(orgId, null, isSuperAdmin);

        return new ApplicationDbContext(options, tenantContext);
    }

    [Fact]
    public async Task OrganizationA_Query_ShouldOnlyReturnOrganizationAData()
    {
        // Arrange
        using var seedContext = CreateContext(null, isSuperAdmin: true);
        await SeedDataAsync(seedContext);

        // Act - Switch to Org A tenant context
        using var orgAContext = CreateContext(_orgAId, isSuperAdmin: false);
        var users = await orgAContext.Users.ToListAsync();

        // Assert
        users.Should().NotBeEmpty();
        users.Should().OnlyContain(u => u.OrganizationId == _orgAId);
        users.Should().NotContain(u => u.OrganizationId == _orgBId);
        users.Should().NotContain(u => u.IsDeleted);
    }

    [Fact]
    public async Task OrganizationB_Query_ShouldNeverAccessOrganizationAData()
    {
        // Arrange
        using var seedContext = CreateContext(null, isSuperAdmin: true);
        await SeedDataAsync(seedContext);

        // Act - Switch to Org B tenant context
        using var orgBContext = CreateContext(_orgBId, isSuperAdmin: false);
        var users = await orgBContext.Users.ToListAsync();

        // Assert
        users.Should().NotBeEmpty();
        users.Should().OnlyContain(u => u.OrganizationId == _orgBId);
        users.Should().NotContain(u => u.OrganizationId == _orgAId);
    }

    [Fact]
    public async Task SuperAdmin_Query_ShouldAccessAllOrganizations()
    {
        // Arrange
        using var seedContext = CreateContext(null, isSuperAdmin: true);
        await SeedDataAsync(seedContext);

        // Act - Query as SuperAdmin
        using var superAdminContext = CreateContext(null, isSuperAdmin: true);
        var users = await superAdminContext.Users.ToListAsync();

        // Assert
        users.Should().HaveCount(2); // userA and userB, deletedUserA filtered out
        users.Should().Contain(u => u.OrganizationId == _orgAId);
        users.Should().Contain(u => u.OrganizationId == _orgBId);
    }
}
