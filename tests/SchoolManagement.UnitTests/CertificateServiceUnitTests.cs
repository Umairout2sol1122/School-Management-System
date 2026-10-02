using System.Text;
using FluentAssertions;
using SchoolManagement.Application.Common.Models;
using SchoolManagement.Domain.Entities.Tenant;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Infrastructure.Services.Certificates;
using Xunit;

namespace SchoolManagement.UnitTests;

public class CertificateServiceUnitTests
{
    [Fact]
    public void PdfDocumentBuilder_BuildCertificatePdf_ProducesValidPdfBytes()
    {
        // Act
        byte[] pdfBytes = PdfDocumentBuilder.BuildCertificatePdf(
            organizationName: "Army Public School & College",
            branchName: "Peshawar Campus",
            certificateTitle: "School Leaving Certificate",
            serialNumber: "SLC-PESH-2026-0001",
            issueDate: new DateTime(2026, 10, 3),
            studentName: "Muhammad Hamza",
            fatherName: "Tariq Mehmood",
            admissionNumber: "ADM-9942",
            rollNumber: "24",
            className: "Class 10",
            sectionName: "A",
            dateOfBirth: new DateTime(2010, 5, 12),
            conduct: "Exemplary",
            leavingReason: "Completed SSC Matriculation Examination",
            promotedToClass: "Promoted to HSSC-I (FSc Pre-Medical)",
            remarks: "Position holder in annual academics and debate captain.",
            verificationCode: "CERT-ABCD1234-20261003",
            verificationUrl: "https://schoolsync.edu.pk/verify-certificate?code=CERT-ABCD1234-20261003",
            issuedByName: "Brigadier (R) Principal");

        // Assert
        pdfBytes.Should().NotBeNull();
        pdfBytes.Length.Should().BeGreaterThan(500);

        string header = Encoding.ASCII.GetString(pdfBytes[..8]);
        header.Should().StartWith("%PDF-1.4");

        string tail = Encoding.ASCII.GetString(pdfBytes[^64..]);
        tail.Should().Contain("%%EOF");
    }

    [Fact]
    public void Certificate_Entity_Initializes_With_Correct_Defaults()
    {
        var cert = new Certificate
        {
            OrganizationId = Guid.NewGuid(),
            StudentId = Guid.NewGuid(),
            CertificateType = CertificateType.TransferCertificate,
            SerialNumber = "TC-MAIN-2026-0001",
            VerificationCode = "CERT-XYZ123-20261003"
        };

        cert.Status.Should().Be(CertificateStatus.Issued);
        cert.IsDeleted.Should().BeFalse();
        cert.IssueDate.Date.Should().Be(DateTime.UtcNow.Date);
    }
}
