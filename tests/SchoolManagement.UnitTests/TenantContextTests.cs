using FluentAssertions;
using SchoolManagement.Application.Common.Models;
using Xunit;

namespace SchoolManagement.UnitTests;

public class TenantContextTests
{
    [Fact]
    public void TenantContext_Default_ShouldBeEmpty()
    {
        // Act
        var context = new TenantContext();

        // Assert
        context.OrganizationId.Should().BeNull();
        context.BranchId.Should().BeNull();
        context.IsSuperAdmin.Should().BeFalse();
    }

    [Fact]
    public void SetTenant_ShouldUpdateOrganizationAndBranch()
    {
        // Arrange
        var context = new TenantContext();
        var orgId = Guid.NewGuid();
        var branchId = Guid.NewGuid();

        // Act
        context.SetTenant(orgId, branchId, false);

        // Assert
        context.OrganizationId.Should().Be(orgId);
        context.BranchId.Should().Be(branchId);
        context.IsSuperAdmin.Should().BeFalse();
    }

    [Fact]
    public void SetTenant_SuperAdmin_ShouldFlagSuperAdminTrue()
    {
        // Arrange
        var context = new TenantContext();

        // Act
        context.SetTenant(null, null, true);

        // Assert
        context.IsSuperAdmin.Should().BeTrue();
    }
}
