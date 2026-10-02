using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.Common.Models;
using SchoolManagement.Domain.Entities.Tenant;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.Infrastructure.Services.Certificates;

public class CertificateService : ICertificateService
{
    private readonly ApplicationDbContext _context;

    public CertificateService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<CertificateClearanceResultDto> CheckClearanceAsync(Guid studentId, CancellationToken cancellationToken = default)
    {
        var student = await _context.Students
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == studentId && !s.IsDeleted, cancellationToken);

        if (student == null)
        {
            throw new KeyNotFoundException($"Student with ID '{studentId}' not found.");
        }

        // Check unpaid fee invoices
        var unpaidInvoices = await _context.FeeInvoices
            .AsNoTracking()
            .Where(f => f.StudentId == studentId && !f.IsDeleted && f.Status != FeeStatus.Paid && f.TotalAmount > f.PaidAmount)
            .OrderBy(f => f.DueDate)
            .Select(f => new UnpaidInvoiceSummaryDto(
                f.Id,
                f.InvoiceNumber,
                f.TotalAmount,
                f.PaidAmount,
                f.TotalAmount - f.PaidAmount,
                f.DueDate))
            .ToListAsync(cancellationToken);

        // Check unreturned library books
        var unreturnedBooks = await _context.BookIssues
            .AsNoTracking()
            .Include(b => b.Book)
            .Where(b => b.StudentId == studentId && !b.IsDeleted && b.Status == BookIssueStatus.Issued)
            .OrderBy(b => b.DueDate)
            .Select(b => new UnreturnedBookSummaryDto(
                b.Id,
                b.Book != null ? b.Book.Title : "Unknown Book",
                b.Book != null && b.Book.Isbn != null ? b.Book.Isbn : "N/A",
                b.IssueDate,
                b.DueDate,
                DateTime.UtcNow > b.DueDate))
            .ToListAsync(cancellationToken);

        decimal totalUnpaid = unpaidInvoices.Sum(i => i.BalanceAmount);
        bool isCleared = unpaidInvoices.Count == 0 && unreturnedBooks.Count == 0;

        string message = isCleared
            ? "Student is fully cleared. No outstanding fee dues or unreturned library books."
            : $"Clearance pending: {unpaidInvoices.Count} unpaid invoices (Total: PKR {totalUnpaid:N2}) and {unreturnedBooks.Count} unreturned library books.";

        return new CertificateClearanceResultDto(
            isCleared,
            totalUnpaid,
            unpaidInvoices.Count,
            unpaidInvoices,
            unreturnedBooks.Count,
            unreturnedBooks,
            message);
    }

    public async Task<CertificateDto> GenerateCertificateAsync(
        GenerateCertificateRequest request,
        Guid currentUserId,
        string currentUserName,
        CancellationToken cancellationToken = default)
    {
        var student = await _context.Students
            .Include(s => s.Branch)
            .Include(s => s.StudentGuardians)
                .ThenInclude(sg => sg.Guardian)
            .FirstOrDefaultAsync(s => s.Id == request.StudentId && !s.IsDeleted, cancellationToken);

        if (student == null)
        {
            throw new KeyNotFoundException($"Student with ID '{request.StudentId}' not found.");
        }

        // Enforce clearance verification for Transfer / School Leaving certificates
        if (request.CertificateType == CertificateType.TransferCertificate ||
            request.CertificateType == CertificateType.SchoolLeavingCertificate)
        {
            if (!request.BypassClearance)
            {
                var clearance = await CheckClearanceAsync(request.StudentId, cancellationToken);
                if (!clearance.IsCleared)
                {
                    throw new InvalidOperationException($"Cannot generate {request.CertificateType}: clearance requirements not met. {clearance.ClearanceMessage}");
                }
            }
        }

        // Generate standardized serial number: {TypePrefix}-{BranchCode}-{Year}-{Seq:D4}
        string typePrefix = request.CertificateType switch
        {
            CertificateType.TransferCertificate => "TC",
            CertificateType.CharacterCertificate => "CC",
            CertificateType.BonafideCertificate => "BC",
            CertificateType.SchoolLeavingCertificate => "SLC",
            CertificateType.SportsCertificate => "SC",
            CertificateType.MeritCertificate => "MC",
            _ => "CERT"
        };

        string branchCode = student.Branch?.Code?.Trim().ToUpperInvariant() ?? "MAIN";
        int year = DateTime.UtcNow.Year;

        int existingCount = await _context.Certificates
            .CountAsync(c => c.OrganizationId == student.OrganizationId &&
                             c.CertificateType == request.CertificateType &&
                             c.IssueDate.Year == year, cancellationToken);

        string serialNumber = $"{typePrefix}-{branchCode}-{year}-{(existingCount + 1):D4}";
        string verificationCode = $"CERT-{Guid.NewGuid():N}"[..17].ToUpperInvariant() + $"-{DateTime.UtcNow:yyyyMMdd}";

        var cert = new Certificate
        {
            OrganizationId = student.OrganizationId,
            BranchId = student.BranchId,
            StudentId = student.Id,
            CertificateType = request.CertificateType,
            SerialNumber = serialNumber,
            Status = CertificateStatus.Issued,
            IssueDate = DateTime.UtcNow,
            IssuedByUserId = currentUserId,
            IssuedByName = currentUserName,
            VerificationCode = verificationCode,
            Conduct = request.Conduct ?? "Good",
            LeavingReason = request.LeavingReason,
            PromotedToClass = request.PromotedToClass,
            Remarks = request.Remarks ?? (request.BypassClearance && !string.IsNullOrWhiteSpace(request.BypassReason) ? $"Clearance bypassed: {request.BypassReason}" : null)
        };

        _context.Certificates.Add(cert);
        await _context.SaveChangesAsync(cancellationToken);

        string? fatherName = student.StudentGuardians
            .FirstOrDefault(sg => sg.Relationship.Equals("Father", StringComparison.OrdinalIgnoreCase))?.Guardian?.FullName
            ?? student.StudentGuardians.FirstOrDefault()?.Guardian?.FullName
            ?? student.EmergencyContactName;

        return new CertificateDto(
            cert.Id,
            cert.OrganizationId,
            cert.BranchId,
            cert.StudentId,
            student.FullName,
            student.AdmissionNumber,
            student.RollNumber,
            student.CurrentGrade,
            student.Section,
            fatherName,
            cert.CertificateType,
            cert.CertificateType.ToString(),
            cert.SerialNumber,
            cert.Status,
            cert.Status.ToString(),
            cert.IssueDate,
            cert.IssuedByName,
            cert.VerificationCode,
            cert.Conduct,
            cert.LeavingReason,
            cert.PromotedToClass,
            cert.Remarks,
            cert.RevocationReason,
            cert.RevokedAt,
            true);
    }

    public async Task<byte[]> GenerateCertificatePdfAsync(Guid certificateId, CancellationToken cancellationToken = default)
    {
        var cert = await _context.Certificates
            .Include(c => c.Student)
                .ThenInclude(s => s!.StudentGuardians)
                    .ThenInclude(sg => sg.Guardian)
            .Include(c => c.Organization)
            .Include(c => c.Branch)
            .FirstOrDefaultAsync(c => c.Id == certificateId && !c.IsDeleted, cancellationToken);

        if (cert == null)
        {
            throw new KeyNotFoundException($"Certificate with ID '{certificateId}' not found.");
        }

        var student = cert.Student ?? throw new InvalidOperationException("Certificate student information is missing.");
        string orgName = cert.Organization?.Name ?? "School Management System";
        string? branchName = cert.Branch?.Name;

        string? fatherName = student.StudentGuardians
            .FirstOrDefault(sg => sg.Relationship.Equals("Father", StringComparison.OrdinalIgnoreCase))?.Guardian?.FullName
            ?? student.StudentGuardians.FirstOrDefault()?.Guardian?.FullName
            ?? student.EmergencyContactName;

        string title = cert.CertificateType switch
        {
            CertificateType.TransferCertificate => "Transfer Certificate",
            CertificateType.CharacterCertificate => "Character Certificate",
            CertificateType.BonafideCertificate => "Bonafide Certificate",
            CertificateType.SchoolLeavingCertificate => "School Leaving Certificate",
            CertificateType.SportsCertificate => "Certificate of Sports Merit",
            CertificateType.MeritCertificate => "Certificate of Academic Excellence",
            _ => "Certificate of Attestation"
        };

        string verificationUrl = $"https://schoolsync.edu.pk/verify-certificate?code={cert.VerificationCode}";

        return PdfDocumentBuilder.BuildCertificatePdf(
            organizationName: orgName,
            branchName: branchName,
            certificateTitle: title,
            serialNumber: cert.SerialNumber,
            issueDate: cert.IssueDate,
            studentName: student.FullName,
            fatherName: fatherName,
            admissionNumber: student.AdmissionNumber,
            rollNumber: student.RollNumber,
            className: student.CurrentGrade,
            sectionName: student.Section,
            dateOfBirth: student.DateOfBirth,
            conduct: cert.Conduct ?? "Good",
            leavingReason: cert.LeavingReason,
            promotedToClass: cert.PromotedToClass,
            remarks: cert.Remarks,
            verificationCode: cert.VerificationCode,
            verificationUrl: verificationUrl,
            issuedByName: cert.IssuedByName);
    }

    public async Task<CertificateVerificationDto?> VerifyCertificateAsync(string verificationCode, CancellationToken cancellationToken = default)
    {
        var cleanCode = verificationCode.Trim();
        var cert = await _context.Certificates
            .IgnoreQueryFilters()
            .Include(c => c.Student)
                .ThenInclude(s => s!.StudentGuardians)
                    .ThenInclude(sg => sg.Guardian)
            .Include(c => c.Organization)
            .Include(c => c.Branch)
            .FirstOrDefaultAsync(c => c.VerificationCode == cleanCode && !c.IsDeleted, cancellationToken);

        if (cert == null) return null;

        var student = cert.Student;
        string? fatherName = student?.StudentGuardians
            .FirstOrDefault(sg => sg.Relationship.Equals("Father", StringComparison.OrdinalIgnoreCase))?.Guardian?.FullName
            ?? student?.StudentGuardians.FirstOrDefault()?.Guardian?.FullName
            ?? student?.EmergencyContactName;

        string typeName = cert.CertificateType switch
        {
            CertificateType.TransferCertificate => "Transfer Certificate",
            CertificateType.CharacterCertificate => "Character Certificate",
            CertificateType.BonafideCertificate => "Bonafide Certificate",
            CertificateType.SchoolLeavingCertificate => "School Leaving Certificate",
            CertificateType.SportsCertificate => "Sports Certificate",
            CertificateType.MeritCertificate => "Merit Certificate",
            _ => cert.CertificateType.ToString()
        };

        return new CertificateVerificationDto(
            IsValid: cert.Status == CertificateStatus.Issued,
            SerialNumber: cert.SerialNumber,
            CertificateType: typeName,
            StudentName: student?.FullName ?? "Unknown Student",
            FatherName: fatherName,
            AdmissionNumber: student?.AdmissionNumber ?? "N/A",
            ClassName: student?.CurrentGrade,
            OrganizationName: cert.Organization?.Name ?? "School Management System",
            BranchName: cert.Branch?.Name,
            IssueDate: cert.IssueDate,
            Status: cert.Status.ToString(),
            IsRevoked: cert.Status == CertificateStatus.Revoked,
            RevocationReason: cert.RevocationReason,
            VerificationUrl: $"https://schoolsync.edu.pk/verify-certificate?code={cert.VerificationCode}");
    }

    public async Task<bool> RevokeCertificateAsync(Guid certificateId, string reason, Guid currentUserId, CancellationToken cancellationToken = default)
    {
        var cert = await _context.Certificates
            .FirstOrDefaultAsync(c => c.Id == certificateId && !c.IsDeleted, cancellationToken);

        if (cert == null) return false;

        cert.Status = CertificateStatus.Revoked;
        cert.RevocationReason = reason;
        cert.RevokedAt = DateTime.UtcNow;
        cert.RevokedByUserId = currentUserId;
        cert.MarkUpdated(currentUserId.ToString());

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<List<CertificateDto>> GetStudentCertificatesAsync(Guid studentId, CancellationToken cancellationToken = default)
    {
        var certs = await _context.Certificates
            .Include(c => c.Student)
                .ThenInclude(s => s!.StudentGuardians)
                    .ThenInclude(sg => sg.Guardian)
            .Where(c => c.StudentId == studentId && !c.IsDeleted)
            .OrderByDescending(c => c.IssueDate)
            .ToListAsync(cancellationToken);

        return certs.Select(MapToDto).ToList();
    }

    public async Task<List<CertificateDto>> GetCertificatesAsync(Guid? branchId = null, CertificateType? type = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Certificates
            .Include(c => c.Student)
                .ThenInclude(s => s!.StudentGuardians)
                    .ThenInclude(sg => sg.Guardian)
            .Where(c => !c.IsDeleted);

        if (branchId.HasValue)
        {
            query = query.Where(c => c.BranchId == branchId.Value);
        }

        if (type.HasValue)
        {
            query = query.Where(c => c.CertificateType == type.Value);
        }

        var certs = await query
            .OrderByDescending(c => c.IssueDate)
            .Take(200)
            .ToListAsync(cancellationToken);

        return certs.Select(MapToDto).ToList();
    }

    private static CertificateDto MapToDto(Certificate c)
    {
        var s = c.Student;
        string? fatherName = s?.StudentGuardians
            .FirstOrDefault(sg => sg.Relationship.Equals("Father", StringComparison.OrdinalIgnoreCase))?.Guardian?.FullName
            ?? s?.StudentGuardians.FirstOrDefault()?.Guardian?.FullName
            ?? s?.EmergencyContactName;

        return new CertificateDto(
            c.Id,
            c.OrganizationId,
            c.BranchId,
            c.StudentId,
            s?.FullName ?? "Unknown",
            s?.AdmissionNumber ?? "N/A",
            s?.RollNumber,
            s?.CurrentGrade,
            s?.Section,
            fatherName,
            c.CertificateType,
            c.CertificateType.ToString(),
            c.SerialNumber,
            c.Status,
            c.Status.ToString(),
            c.IssueDate,
            c.IssuedByName,
            c.VerificationCode,
            c.Conduct,
            c.LeavingReason,
            c.PromotedToClass,
            c.Remarks,
            c.RevocationReason,
            c.RevokedAt,
            true);
    }
}
