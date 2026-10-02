using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolManagement.Api.Authorization;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.Common.Models;
using SchoolManagement.Domain.Constants;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CertificatesController : BaseApiController
{
    private readonly ICertificateService _certificateService;
    private readonly IAuditService _auditService;

    public CertificatesController(
        ICertificateService certificateService,
        IAuditService auditService)
    {
        _certificateService = certificateService;
        _auditService = auditService;
    }

    [HttpGet]
    [RequirePermission(Permissions.StudentsView)]
    public async Task<IActionResult> GetCertificates(
        [FromQuery] Guid? branchId,
        [FromQuery] CertificateType? type,
        CancellationToken cancellationToken = default)
    {
        var certs = await _certificateService.GetCertificatesAsync(branchId, type, cancellationToken);
        return ApiSuccess(certs);
    }

    [HttpGet("student/{studentId:guid}")]
    [RequirePermission(Permissions.StudentsView)]
    public async Task<IActionResult> GetStudentCertificates(
        Guid studentId,
        CancellationToken cancellationToken = default)
    {
        var certs = await _certificateService.GetStudentCertificatesAsync(studentId, cancellationToken);
        return ApiSuccess(certs);
    }

    [HttpGet("clearance/{studentId:guid}")]
    [RequirePermission(Permissions.StudentsView)]
    public async Task<IActionResult> CheckClearance(
        Guid studentId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _certificateService.CheckClearanceAsync(studentId, cancellationToken);
            return ApiSuccess(result);
        }
        catch (KeyNotFoundException ex)
        {
            return ApiError(ex.Message, StatusCodes.Status404NotFound);
        }
    }

    [HttpPost("generate")]
    [RequirePermission(Permissions.StudentsEdit)]
    public async Task<IActionResult> GenerateCertificate(
        [FromBody] GenerateCertificateRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // If bypass is requested, verify if user has admin permission or record audit
            if (request.BypassClearance && string.IsNullOrWhiteSpace(request.BypassReason))
            {
                return ApiError("A valid bypass reason is required when overriding clearance requirements.", StatusCodes.Status400BadRequest);
            }

            var userId = CurrentUserService.UserId ?? Guid.Empty;
            var userName = CurrentUserService.Email ?? "Staff";

            var cert = await _certificateService.GenerateCertificateAsync(request, userId, userName, cancellationToken);

            await _auditService.LogAsync(
                AuditAction.Create,
                "Certificates",
                "Certificate",
                cert.Id.ToString(),
                $"Generated {cert.CertificateType} (Serial: {cert.SerialNumber}) for Student {cert.StudentName} (Admission: {cert.AdmissionNumber}). Bypass: {request.BypassClearance}",
                userId: userId,
                userEmail: userName,
                cancellationToken: cancellationToken);

            return StatusCode(StatusCodes.Status201Created, new
            {
                success = true,
                message = $"{cert.CertificateTypeName} generated successfully.",
                data = cert,
                timestampUtc = DateTime.UtcNow
            });
        }
        catch (KeyNotFoundException ex)
        {
            return ApiError(ex.Message, StatusCodes.Status404NotFound);
        }
        catch (InvalidOperationException ex)
        {
            // Clearance failure
            var clearance = await _certificateService.CheckClearanceAsync(request.StudentId, cancellationToken);
            return ApiError(ex.Message, StatusCodes.Status422UnprocessableEntity, clearance);
        }
    }

    [HttpGet("{id:guid}/pdf")]
    [RequirePermission(Permissions.StudentsView)]
    public async Task<IActionResult> DownloadCertificatePdf(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var pdfBytes = await _certificateService.GenerateCertificatePdfAsync(id, cancellationToken);

            var userId = CurrentUserService.UserId ?? Guid.Empty;
            await _auditService.LogAsync(
                AuditAction.Export,
                "Certificates",
                "Certificate",
                id.ToString(),
                $"Downloaded PDF for Certificate {id}",
                userId: userId,
                userEmail: CurrentUserService.Email,
                cancellationToken: cancellationToken);

            return File(pdfBytes, "application/pdf", $"Certificate-{id}.pdf");
        }
        catch (KeyNotFoundException ex)
        {
            return ApiError(ex.Message, StatusCodes.Status404NotFound);
        }
    }

    [HttpPost("{id:guid}/revoke")]
    [RequirePermission(Permissions.StudentsEdit)]
    public async Task<IActionResult> RevokeCertificate(
        Guid id,
        [FromBody] RevokeCertificateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return ApiError("Revocation reason is required.", StatusCodes.Status400BadRequest);
        }

        var userId = CurrentUserService.UserId ?? Guid.Empty;
        bool revoked = await _certificateService.RevokeCertificateAsync(id, request.Reason, userId, cancellationToken);

        if (!revoked)
        {
            return ApiError("Certificate not found or already revoked.", StatusCodes.Status404NotFound);
        }

        await _auditService.LogAsync(
            AuditAction.Update,
            "Certificates",
            "Certificate",
            id.ToString(),
            $"Revoked Certificate {id}. Reason: {request.Reason}",
            userId: userId,
            userEmail: CurrentUserService.Email,
            cancellationToken: cancellationToken);

        return ApiSuccess("Certificate revoked successfully.");
    }

    [AllowAnonymous]
    [HttpGet("verify/{verificationCode}")]
    public async Task<IActionResult> VerifyCertificate(
        string verificationCode,
        CancellationToken cancellationToken = default)
    {
        var result = await _certificateService.VerifyCertificateAsync(verificationCode, cancellationToken);
        if (result == null)
        {
            return ApiError("Certificate not found or invalid verification code.", StatusCodes.Status404NotFound);
        }

        return ApiSuccess(result);
    }
}
