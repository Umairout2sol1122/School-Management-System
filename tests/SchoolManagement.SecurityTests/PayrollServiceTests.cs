using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Common.Models;
using SchoolManagement.Domain.Entities.Security;
using SchoolManagement.Domain.Entities.Staff;
using SchoolManagement.Domain.Entities.Tenant;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.Infrastructure.Services.Payroll;
using Xunit;

namespace SchoolManagement.SecurityTests;

public class PayrollServiceTests
{
    private ApplicationDbContext CreateInMemoryContext(string dbName, Guid orgId)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        var tenantContext = new TenantContext();
        tenantContext.SetTenant(orgId, null, false);

        return new ApplicationDbContext(options, tenantContext);
    }

    [Fact]
    public async Task SetStaffSalaryStructureAsync_CreatesActiveSalaryStructure_WithCalculatedGross()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var context = CreateInMemoryContext(Guid.NewGuid().ToString(), orgId);
        var staffId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        context.Users.Add(new User
        {
            Id = userId,
            OrganizationId = orgId,
            FirstName = "Tariq",
            LastName = "Mehmood",
            Email = "tariq@school.pk",
            RoleType = UserRoleType.Teacher
        });

        context.StaffProfiles.Add(new StaffProfile
        {
            Id = staffId,
            OrganizationId = orgId,
            UserId = userId,
            EmployeeCode = "EMP-042",
            Designation = "Senior Lecturer Physics",
            Department = "Science"
        });

        await context.SaveChangesAsync();
        var service = new PayrollService(context);

        var request = new CreateSalaryStructureRequest(
            StaffProfileId: staffId,
            BasicSalary: 60000m,
            HouseRentAllowance: 27000m, // 45%
            MedicalAllowance: 6000m,    // 10%
            ConveyanceAllowance: 7000m,
            SpecialAllowance: 5000m,
            BankName: "Habib Bank Limited",
            BankAccountNumber: "00421008920192");

        // Act
        var result = await service.SetStaffSalaryStructureAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.BasicSalary.Should().Be(60000m);
        result.GrossSalary.Should().Be(105000m); // 60k + 27k + 6k + 7k + 5k
        result.BankName.Should().Be("Habib Bank Limited");
        result.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task GenerateMonthlyPayrollAsync_BatchProcessesStaff_AndCalculatesTaxesCorrectly()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var context = CreateInMemoryContext(Guid.NewGuid().ToString(), orgId);

        var branch = new Branch { Id = branchId, OrganizationId = orgId, Name = "Main Campus", Code = "ISB" };
        context.Branches.Add(branch);

        // Staff 1: Gross 150k/mo -> Taxable 1.8M/yr -> 10k monthly tax
        var staff1Id = Guid.NewGuid();
        var user1Id = Guid.NewGuid();
        context.Users.Add(new User { Id = user1Id, OrganizationId = orgId, FirstName = "Dr.", LastName = "Asad", Email = "asad@school.pk" });
        context.StaffProfiles.Add(new StaffProfile
        {
            Id = staff1Id,
            OrganizationId = orgId,
            BranchId = branchId,
            UserId = user1Id,
            EmployeeCode = "FAC-01",
            Designation = "Principal",
            Department = "Administration",
            Status = StaffStatus.Active
        });
        context.SalaryStructures.Add(new SalaryStructure
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            BranchId = branchId,
            StaffProfileId = staff1Id,
            BasicSalary = 90000m,
            HouseRentAllowance = 40500m,
            MedicalAllowance = 9500m,
            ConveyanceAllowance = 10000m,
            SpecialAllowance = 0m,
            IsActive = true
        });

        // Staff 2: Gross 45k/mo -> Taxable 540k/yr -> 0 tax (Exempt)
        var staff2Id = Guid.NewGuid();
        var user2Id = Guid.NewGuid();
        context.Users.Add(new User { Id = user2Id, OrganizationId = orgId, FirstName = "Bilal", LastName = "Khan", Email = "bilal@school.pk" });
        context.StaffProfiles.Add(new StaffProfile
        {
            Id = staff2Id,
            OrganizationId = orgId,
            BranchId = branchId,
            UserId = user2Id,
            EmployeeCode = "SUP-02",
            Designation = "Lab Assistant",
            Department = "Science",
            Status = StaffStatus.Active
        });
        context.SalaryStructures.Add(new SalaryStructure
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            BranchId = branchId,
            StaffProfileId = staff2Id,
            BasicSalary = 30000m,
            HouseRentAllowance = 10000m,
            MedicalAllowance = 2500m,
            ConveyanceAllowance = 2500m,
            IsActive = true
        });

        await context.SaveChangesAsync();
        var service = new PayrollService(context);

        var request = new GeneratePayrollRunRequest(
            Month: 10,
            Year: 2026,
            BranchId: branchId,
            Title: "October 2026 Faculty Payroll");

        // Act
        var run = await service.GenerateMonthlyPayrollAsync(request, Guid.NewGuid(), "HR Manager");

        // Assert
        run.Should().NotBeNull();
        run.TotalStaffCount.Should().Be(2);
        run.Status.Should().Be(PayrollStatus.Draft);
        run.TotalGrossSalary.Should().Be(195000m); // 150k + 45k
        run.TotalTaxDeduction.Should().Be(10000m);  // 10k for Staff 1, 0 for Staff 2
        run.TotalEobiDeduction.Should().Be(740m);   // 370 * 2

        var slips = await service.GetPaySlipsForRunAsync(run.Id);
        slips.Should().HaveCount(2);

        var slip1 = slips.First(s => s.StaffProfileId == staff1Id);
        slip1.SlipNumber.Should().StartWith("PS-ISB-202610-");
        slip1.GrossSalary.Should().Be(150000m);
        slip1.TaxDeduction.Should().Be(10000m);
        slip1.EobiEmployeeContribution.Should().Be(370m);
        slip1.NetSalary.Should().Be(150000m - 10000m - 370m); // 139,630

        var slip2 = slips.First(s => s.StaffProfileId == staff2Id);
        slip2.GrossSalary.Should().Be(45000m);
        slip2.TaxDeduction.Should().Be(0m);
        slip2.EobiEmployeeContribution.Should().Be(370m);
        slip2.NetSalary.Should().Be(45000m - 370m); // 44,630
    }

    [Fact]
    public async Task Approve_And_DisbursePayrollRunAsync_TransitionsStatusCorrectly()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var context = CreateInMemoryContext(Guid.NewGuid().ToString(), orgId);

        var staffId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        context.Users.Add(new User { Id = user1Id_generate(userId), OrganizationId = orgId, FirstName = "Admin", LastName = "User" });
        context.StaffProfiles.Add(new StaffProfile
        {
            Id = staffId,
            OrganizationId = orgId,
            UserId = userId,
            EmployeeCode = "EMP-01",
            Status = StaffStatus.Active,
            BasicSalary = 50000m
        });
        await context.SaveChangesAsync();

        var service = new PayrollService(context);
        var run = await service.GenerateMonthlyPayrollAsync(
            new GeneratePayrollRunRequest(Month: 9, Year: 2026),
            Guid.NewGuid(),
            "Finance Manager");

        run.Status.Should().Be(PayrollStatus.Draft);

        // Act 1: Approve
        bool approved = await service.ApprovePayrollRunAsync(run.Id, Guid.NewGuid(), "Finance Director");
        approved.Should().BeTrue();

        var approvedRun = await service.GetPayrollRunByIdAsync(run.Id);
        approvedRun!.Status.Should().Be(PayrollStatus.Approved);

        // Act 2: Disburse
        bool disbursed = await service.DisbursePayrollRunAsync(
            run.Id,
            new DisbursePayrollRequest(SalaryPaymentMode.BankTransfer, "BATCH-FT-20260930"),
            Guid.NewGuid(),
            "Cashier Officer");
        disbursed.Should().BeTrue();

        var disbursedRun = await service.GetPayrollRunByIdAsync(run.Id);
        disbursedRun!.Status.Should().Be(PayrollStatus.Disbursed);

        var slips = await service.GetPaySlipsForRunAsync(run.Id);
        slips.Should().AllSatisfy(s =>
        {
            s.Status.Should().Be(PaySlipStatus.Paid);
            s.PaidDate.Should().NotBeNull();
        });
    }

    private static Guid user1Id_generate(Guid id) => id;
}
