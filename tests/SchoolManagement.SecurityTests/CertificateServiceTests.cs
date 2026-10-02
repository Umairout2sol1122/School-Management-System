using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Common.Models;
using SchoolManagement.Domain.Entities.Academics;
using SchoolManagement.Domain.Entities.Student;
using SchoolManagement.Domain.Entities.Tenant;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.Infrastructure.Services.Certificates;
using Xunit;

namespace SchoolManagement.SecurityTests;

public class CertificateServiceTests
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
    public async Task CheckClearanceAsync_WhenStudentHasNoDuesAndNoBooks_ReturnsIsClearedTrue()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var context = CreateInMemoryContext(Guid.NewGuid().ToString(), orgId);
        var studentId = Guid.NewGuid();

        var student = new Student
        {
            Id = studentId,
            OrganizationId = orgId,
            FirstName = "Zain",
            LastName = "Abbas",
            AdmissionNumber = "ADM-101",
            CurrentGrade = "Class 9"
        };
        context.Students.Add(student);

        // Add a paid fee invoice
        context.FeeInvoices.Add(new FeeInvoice
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            StudentId = studentId,
            InvoiceNumber = "INV-001",
            TotalAmount = 8000m,
            PaidAmount = 8000m,
            Status = FeeStatus.Paid
        });

        await context.SaveChangesAsync();
        var service = new CertificateService(context);

        // Act
        var result = await service.CheckClearanceAsync(studentId);

        // Assert
        result.IsCleared.Should().BeTrue();
        result.UnpaidFeeCount.Should().Be(0);
        result.TotalUnpaidFees.Should().Be(0);
        result.UnreturnedBookCount.Should().Be(0);
        result.ClearanceMessage.Should().Contain("fully cleared");
    }

    [Fact]
    public async Task CheckClearanceAsync_WhenStudentHasUnpaidInvoice_ReturnsIsClearedFalseWithBreakdown()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var context = CreateInMemoryContext(Guid.NewGuid().ToString(), orgId);
        var studentId = Guid.NewGuid();

        context.Students.Add(new Student
        {
            Id = studentId,
            OrganizationId = orgId,
            FirstName = "Bilal",
            LastName = "Ahmed",
            AdmissionNumber = "ADM-102",
            CurrentGrade = "Class 8"
        });

        // Unpaid invoice with balance 4,500
        context.FeeInvoices.Add(new FeeInvoice
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            StudentId = studentId,
            InvoiceNumber = "INV-002",
            TotalAmount = 10000m,
            PaidAmount = 5500m,
            Status = FeeStatus.PartiallyPaid,
            DueDate = DateTime.UtcNow.AddDays(-5)
        });

        await context.SaveChangesAsync();
        var service = new CertificateService(context);

        // Act
        var result = await service.CheckClearanceAsync(studentId);

        // Assert
        result.IsCleared.Should().BeFalse();
        result.UnpaidFeeCount.Should().Be(1);
        result.TotalUnpaidFees.Should().Be(4500m);
        result.UnpaidInvoices.Should().HaveCount(1);
        result.UnpaidInvoices[0].InvoiceNumber.Should().Be("INV-002");
        result.UnpaidInvoices[0].BalanceAmount.Should().Be(4500m);
    }

    [Fact]
    public async Task CheckClearanceAsync_WhenStudentHasUnreturnedLibraryBook_ReturnsIsClearedFalseWithBreakdown()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var context = CreateInMemoryContext(Guid.NewGuid().ToString(), orgId);
        var studentId = Guid.NewGuid();
        var bookId = Guid.NewGuid();

        context.Students.Add(new Student
        {
            Id = studentId,
            OrganizationId = orgId,
            FirstName = "Sara",
            LastName = "Khan",
            AdmissionNumber = "ADM-103",
            CurrentGrade = "Class 7"
        });

        context.Books.Add(new Book
        {
            Id = bookId,
            OrganizationId = orgId,
            Title = "Fundamentals of Physics",
            Isbn = "978-0123456789"
        });

        context.BookIssues.Add(new BookIssue
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            StudentId = studentId,
            BookId = bookId,
            Status = BookIssueStatus.Issued,
            IssueDate = DateTime.UtcNow.AddDays(-20),
            DueDate = DateTime.UtcNow.AddDays(-6)
        });

        await context.SaveChangesAsync();
        var service = new CertificateService(context);

        // Act
        var result = await service.CheckClearanceAsync(studentId);

        // Assert
        result.IsCleared.Should().BeFalse();
        result.UnreturnedBookCount.Should().Be(1);
        result.UnreturnedBooks[0].BookTitle.Should().Be("Fundamentals of Physics");
        result.UnreturnedBooks[0].IsOverdue.Should().BeTrue();
    }

    [Fact]
    public async Task GenerateCertificateAsync_WhenTransferCertAndClearanceFails_ThrowsInvalidOperationException()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var context = CreateInMemoryContext(Guid.NewGuid().ToString(), orgId);
        var studentId = Guid.NewGuid();

        context.Students.Add(new Student
        {
            Id = studentId,
            OrganizationId = orgId,
            FirstName = "Usman",
            LastName = "Ghani",
            AdmissionNumber = "ADM-104",
            CurrentGrade = "Class 10"
        });

        // Unpaid fees
        context.FeeInvoices.Add(new FeeInvoice
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            StudentId = studentId,
            InvoiceNumber = "INV-003",
            TotalAmount = 12000m,
            PaidAmount = 0m,
            Status = FeeStatus.Unpaid
        });

        await context.SaveChangesAsync();
        var service = new CertificateService(context);

        var request = new GenerateCertificateRequest(
            StudentId: studentId,
            CertificateType: CertificateType.TransferCertificate,
            BypassClearance: false);

        // Act
        Func<Task> act = async () => await service.GenerateCertificateAsync(
            request, Guid.NewGuid(), "Admin");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*clearance requirements not met*");
    }

    [Fact]
    public async Task GenerateCertificateAsync_WhenTransferCertAndClearanceBypassed_SucceedsWithFormattedSerial()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var context = CreateInMemoryContext(Guid.NewGuid().ToString(), orgId);
        var studentId = Guid.NewGuid();

        var branch = new Branch
        {
            Id = branchId,
            OrganizationId = orgId,
            Name = "Islamabad Main Campus",
            Code = "ISB"
        };
        context.Branches.Add(branch);

        context.Students.Add(new Student
        {
            Id = studentId,
            OrganizationId = orgId,
            BranchId = branchId,
            FirstName = "Ahmad",
            LastName = "Raza",
            AdmissionNumber = "ADM-105",
            CurrentGrade = "Class 10"
        });

        // Unpaid fee present
        context.FeeInvoices.Add(new FeeInvoice
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            StudentId = studentId,
            InvoiceNumber = "INV-004",
            TotalAmount = 5000m,
            PaidAmount = 0m,
            Status = FeeStatus.Unpaid
        });

        await context.SaveChangesAsync();
        var service = new CertificateService(context);

        var request = new GenerateCertificateRequest(
            StudentId: studentId,
            CertificateType: CertificateType.TransferCertificate,
            Conduct: "Good",
            LeavingReason: "Transfer to Lahore",
            BypassClearance: true,
            BypassReason: "Principal written exemption approved");

        // Act
        var cert = await service.GenerateCertificateAsync(
            request, Guid.NewGuid(), "Vice Principal");

        // Assert
        cert.Should().NotBeNull();
        cert.SerialNumber.Should().StartWith($"TC-ISB-{DateTime.UtcNow.Year}-");
        cert.Status.Should().Be(CertificateStatus.Issued);
        cert.Remarks.Should().Contain("Clearance bypassed: Principal written exemption approved");
        cert.VerificationCode.Should().StartWith("CERT-");
    }

    [Fact]
    public async Task VerifyCertificateAsync_And_RevokeCertificateAsync_WorkTogetherProperly()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var context = CreateInMemoryContext(Guid.NewGuid().ToString(), orgId);
        var studentId = Guid.NewGuid();

        var org = new Organization { Id = orgId, Name = "Beaconhouse School System", Code = "BSS" };
        context.Organizations.Add(org);

        var student = new Student
        {
            Id = studentId,
            OrganizationId = orgId,
            FirstName = "Fatima",
            LastName = "Zahra",
            AdmissionNumber = "ADM-201",
            CurrentGrade = "A-Levels"
        };
        context.Students.Add(student);
        await context.SaveChangesAsync();

        var service = new CertificateService(context);
        var certDto = await service.GenerateCertificateAsync(
            new GenerateCertificateRequest(
                StudentId: studentId,
                CertificateType: CertificateType.CharacterCertificate,
                Conduct: "Exemplary"),
            Guid.NewGuid(),
            "Academic Coordinator");

        // Act 1: Verify Active Certificate
        var verification = await service.VerifyCertificateAsync(certDto.VerificationCode);

        // Assert 1
        verification.Should().NotBeNull();
        verification!.IsValid.Should().BeTrue();
        verification.IsRevoked.Should().BeFalse();
        verification.StudentName.Should().Be("Fatima Zahra");
        verification.OrganizationName.Should().Be("Beaconhouse School System");
        verification.CertificateType.Should().Be("Character Certificate");

        // Act 2: Revoke Certificate
        bool revoked = await service.RevokeCertificateAsync(
            certDto.Id, "Duplicate certificate re-issued under correction", Guid.NewGuid());
        revoked.Should().BeTrue();

        // Act 3: Verify Revoked Certificate
        var revokedVerification = await service.VerifyCertificateAsync(certDto.VerificationCode);

        // Assert 3
        revokedVerification.Should().NotBeNull();
        revokedVerification!.IsValid.Should().BeFalse();
        revokedVerification.IsRevoked.Should().BeTrue();
        revokedVerification.Status.Should().Be("Revoked");
        revokedVerification.RevocationReason.Should().Be("Duplicate certificate re-issued under correction");
    }
}
