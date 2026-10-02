using FluentAssertions;
using SchoolManagement.Application.Common.Models;
using SchoolManagement.Domain.Entities.Tenant;
using Xunit;

namespace SchoolManagement.UnitTests;

public class OrganizationBranchQuotaTests
{
    [Fact]
    public void Organization_DefaultMaxBranches_ShouldBeFive()
    {
        // Act
        var org = new Organization();

        // Assert
        org.MaxBranches.Should().Be(5);
        org.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Organization_MaxBranches_CanBeCustomizedBySuperAdmin()
    {
        // Act
        var org = new Organization
        {
            Name = "Lahore Grammar School",
            Code = "LGS-01",
            MaxBranches = 12
        };

        // Assert
        org.MaxBranches.Should().Be(12);
    }

    [Fact]
    public void CreateOrganizationRequestDto_DefaultMaxBranches_ShouldBeFive()
    {
        // Act
        var dto = new CreateOrganizationRequestDto(
            "Army Public School",
            "APS-01",
            "info@aps.edu.pk",
            "+92 51 1234567",
            "Rawalpindi",
            null,
            null,
            null,
            "admin@aps.edu.pk",
            "apsadmin",
            "Admin@2026!Pak",
            "Tariq",
            "Mahmood"
        );

        // Assert
        dto.MaxBranches.Should().Be(5);
    }

    [Fact]
    public void CreateOrganizationRequestDto_WithCustomMaxBranches_ShouldRetainValue()
    {
        // Act
        var dto = new CreateOrganizationRequestDto(
            "Beaconhouse School System",
            "BSS-01",
            "info@bss.edu.pk",
            "+92 42 1234567",
            "Lahore",
            null,
            null,
            null,
            "admin@bss.edu.pk",
            "bssadmin",
            "Admin@2026!Pak",
            "Kashif",
            "Raza",
            MaxBranches: 10
        );

        // Assert
        dto.MaxBranches.Should().Be(10);
    }

    [Fact]
    public void OrganizationSummaryDto_IncludesMaxBranches()
    {
        // Act
        var summary = new OrganizationSummaryDto(
            Guid.NewGuid(),
            "City School",
            "TCS",
            "tcs@school.edu.pk",
            "+92 42 3571234",
            true,
            2,
            150,
            "#924EC0",
            "#5840BA",
            DateTime.UtcNow,
            MaxBranches: 8
        );

        // Assert
        summary.MaxBranches.Should().Be(8);
        summary.BranchesCount.Should().Be(2);
    }
}
