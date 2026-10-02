using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Api.Authorization;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.Common.Models;
using SchoolManagement.Domain.Constants;
using SchoolManagement.Domain.Entities.Academics;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DocumentsController : BaseApiController
{
    private readonly IApplicationDbContext _context;
    private readonly IAuditService _auditService;

    public DocumentsController(IApplicationDbContext context, IAuditService auditService)
    {
        _context = context;
        _auditService = auditService;
    }

    private Guid GetTargetOrgId()
    {
        return CurrentUserService.IsSuperAdmin
            ? (TenantContext.OrganizationId ?? Guid.Empty)
            : (CurrentUserService.OrganizationId ?? Guid.Empty);
    }

    [HttpGet]
    public async Task<IActionResult> GetDocuments(
        [FromQuery] Guid? studentId,
        [FromQuery] StudentDocumentType? documentType,
        [FromQuery] Guid? branchId,
        [FromQuery] string? search,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        Guid targetOrgId = GetTargetOrgId();
        if (targetOrgId == Guid.Empty)
            return ApiError("Tenant context required.", StatusCodes.Status400BadRequest);

        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _context.StudentDocuments
            .Include(d => d.Student)
            .Include(d => d.Branch)
            .Include(d => d.UploadedByUser)
            .Where(d => d.OrganizationId == targetOrgId && !d.IsDeleted);

        // Role-based scoping
        if (CurrentUserService.RoleType == UserRoleType.Student)
        {
            query = query.Where(d => d.Student != null && d.Student.UserId == CurrentUserService.UserId);
        }
        else if (CurrentUserService.RoleType == UserRoleType.Parent)
        {
            var childIds = await _context.StudentGuardians
                .Where(sg => sg.Guardian != null && sg.Guardian.UserId == CurrentUserService.UserId && !sg.IsDeleted)
                .Select(sg => sg.StudentId)
                .ToListAsync(cancellationToken);

            query = query.Where(d => childIds.Contains(d.StudentId));
        }
        else if (CurrentUserService.RoleType == UserRoleType.Principal && CurrentUserService.BranchId.HasValue)
        {
            query = query.Where(d => d.BranchId == CurrentUserService.BranchId.Value);
        }
        else if (branchId.HasValue)
        {
            query = query.Where(d => d.BranchId == branchId.Value);
        }

        if (studentId.HasValue)
            query = query.Where(d => d.StudentId == studentId.Value);

        if (documentType.HasValue)
            query = query.Where(d => d.DocumentType == documentType.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            string term = search.Trim().ToLower();
            query = query.Where(d =>
                d.Title.ToLower().Contains(term) ||
                d.FileName.ToLower().Contains(term) ||
                (d.Student != null && (
                    d.Student.FirstName.ToLower().Contains(term) ||
                    d.Student.LastName.ToLower().Contains(term) ||
                    d.Student.AdmissionNumber.ToLower().Contains(term)
                )));
        }

        int totalCount = await query.CountAsync(cancellationToken);

        var documents = await query
            .OrderByDescending(d => d.CreatedAtUtc)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = documents.Select(d => new StudentDocumentItemDto(
            d.Id,
            d.StudentId,
            d.Student?.FullName ?? "Unknown",
            d.Student?.AdmissionNumber ?? "-",
            d.Student?.CurrentGrade ?? "-",
            d.BranchId,
            d.Branch?.Name,
            d.Title,
            d.DocumentType,
            d.DocumentType.ToString(),
            d.FileUrl,
            d.FileName,
            d.FileSizeBytes,
            d.Description,
            d.CreatedAtUtc,
            d.UploadedByUser?.FullName
        )).ToList();

        return ApiSuccess(new
        {
            items,
            pageNumber,
            pageSize,
            totalCount,
            totalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetDocumentById(Guid id, CancellationToken cancellationToken)
    {
        Guid targetOrgId = GetTargetOrgId();
        if (targetOrgId == Guid.Empty)
            return ApiError("Tenant context required.", StatusCodes.Status400BadRequest);

        var doc = await _context.StudentDocuments
            .Include(d => d.Student)
            .Include(d => d.Branch)
            .Include(d => d.UploadedByUser)
            .FirstOrDefaultAsync(d => d.Id == id && d.OrganizationId == targetOrgId && !d.IsDeleted, cancellationToken);

        if (doc == null)
            return ApiNotFound("Document not found.");

        // Role authorization check
        if (CurrentUserService.RoleType == UserRoleType.Student)
        {
            if (doc.Student?.UserId != CurrentUserService.UserId)
                return ApiForbidden("You do not have permission to view documents of other students.");
        }
        else if (CurrentUserService.RoleType == UserRoleType.Parent)
        {
            bool isGuardian = await _context.StudentGuardians
                .AnyAsync(sg => sg.StudentId == doc.StudentId && sg.Guardian != null && sg.Guardian.UserId == CurrentUserService.UserId && !sg.IsDeleted, cancellationToken);
            if (!isGuardian)
                return ApiForbidden("You do not have permission to view documents of this student.");
        }
        else if (CurrentUserService.RoleType == UserRoleType.Principal && CurrentUserService.BranchId.HasValue)
        {
            if (doc.BranchId != CurrentUserService.BranchId.Value)
                return ApiForbidden("You do not have permission to view documents outside your assigned branch.");
        }

        var dto = new StudentDocumentItemDto(
            doc.Id,
            doc.StudentId,
            doc.Student?.FullName ?? "Unknown",
            doc.Student?.AdmissionNumber ?? "-",
            doc.Student?.CurrentGrade ?? "-",
            doc.BranchId,
            doc.Branch?.Name,
            doc.Title,
            doc.DocumentType,
            doc.DocumentType.ToString(),
            doc.FileUrl,
            doc.FileName,
            doc.FileSizeBytes,
            doc.Description,
            doc.CreatedAtUtc,
            doc.UploadedByUser?.FullName
        );

        return ApiSuccess(dto);
    }

    [HttpPost]
    [RequirePermission(Permissions.StudentsEdit)]
    public async Task<IActionResult> UploadDocument([FromBody] UploadDocumentRequestDto request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return ApiError("Document title is required.");

        if (string.IsNullOrWhiteSpace(request.FileUrl))
            return ApiError("File URL is required.");

        Guid targetOrgId = GetTargetOrgId();
        if (targetOrgId == Guid.Empty)
            return ApiError("Tenant context required.", StatusCodes.Status400BadRequest);

        var student = await _context.Students
            .FirstOrDefaultAsync(s => s.Id == request.StudentId && s.OrganizationId == targetOrgId && !s.IsDeleted, cancellationToken);

        if (student == null)
            return ApiNotFound("Student not found.");

        if (!CurrentUserService.IsSuperAdmin && CurrentUserService.BranchId.HasValue && CurrentUserService.RoleType == UserRoleType.Principal)
        {
            if (student.BranchId != CurrentUserService.BranchId.Value)
                return ApiForbidden("You cannot upload documents for students in another branch.");
        }

        var doc = new StudentDocument
        {
            Id = Guid.NewGuid(),
            OrganizationId = targetOrgId,
            BranchId = student.BranchId,
            StudentId = student.Id,
            Title = request.Title.Trim(),
            DocumentType = request.DocumentType,
            FileUrl = request.FileUrl.Trim(),
            FileName = string.IsNullOrWhiteSpace(request.FileName) ? "document" : request.FileName.Trim(),
            FileSizeBytes = request.FileSizeBytes,
            Description = request.Description?.Trim(),
            UploadedByUserId = CurrentUserService.UserId
        };

        _context.StudentDocuments.Add(doc);
        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            AuditAction.Create, "Documents", "StudentDocument", doc.Id.ToString(),
            $"Uploaded {doc.DocumentType} document '{doc.Title}' for student '{student.FullName}' ({student.AdmissionNumber})",
            organizationId: targetOrgId,
            branchId: doc.BranchId,
            userId: CurrentUserService.UserId,
            userEmail: CurrentUserService.Email,
            cancellationToken: cancellationToken);

        return ApiSuccess(new { id = doc.Id, title = doc.Title, fileUrl = doc.FileUrl }, "Document registered and secured successfully.");
    }

    [HttpGet("{id:guid}/download")]
    public async Task<IActionResult> DownloadDocument(Guid id, CancellationToken cancellationToken)
    {
        Guid targetOrgId = GetTargetOrgId();
        if (targetOrgId == Guid.Empty)
            return ApiError("Tenant context required.", StatusCodes.Status400BadRequest);

        var doc = await _context.StudentDocuments
            .Include(d => d.Student)
            .FirstOrDefaultAsync(d => d.Id == id && d.OrganizationId == targetOrgId && !d.IsDeleted, cancellationToken);

        if (doc == null)
            return ApiNotFound("Document not found.");

        // Strict download security authorization
        if (CurrentUserService.RoleType == UserRoleType.Student)
        {
            if (doc.Student?.UserId != CurrentUserService.UserId)
                return ApiForbidden("Direct download forbidden: You can only download your own student documents.");
        }
        else if (CurrentUserService.RoleType == UserRoleType.Parent)
        {
            bool isGuardian = await _context.StudentGuardians
                .AnyAsync(sg => sg.StudentId == doc.StudentId && sg.Guardian != null && sg.Guardian.UserId == CurrentUserService.UserId && !sg.IsDeleted, cancellationToken);
            if (!isGuardian)
                return ApiForbidden("Direct download forbidden: You can only download documents for your registered children.");
        }
        else if (CurrentUserService.RoleType == UserRoleType.Principal && CurrentUserService.BranchId.HasValue)
        {
            if (doc.BranchId != CurrentUserService.BranchId.Value)
                return ApiForbidden("Direct download forbidden: Document belongs to another campus branch.");
        }

        await _auditService.LogAsync(
            AuditAction.View, "Documents", "StudentDocument", doc.Id.ToString(),
            $"User downloaded document '{doc.Title}' ({doc.FileName}) for student '{doc.Student?.FullName}'",
            organizationId: targetOrgId,
            branchId: doc.BranchId,
            userId: CurrentUserService.UserId,
            userEmail: CurrentUserService.Email,
            cancellationToken: cancellationToken);

        return ApiSuccess(new
        {
            downloadUrl = doc.FileUrl,
            fileName = doc.FileName,
            fileSize = doc.FileSizeBytes,
            contentType = "application/octet-stream"
        }, "Document download link authorized.");
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(Permissions.StudentsDelete)]
    public async Task<IActionResult> DeleteDocument(Guid id, CancellationToken cancellationToken)
    {
        Guid targetOrgId = GetTargetOrgId();
        if (targetOrgId == Guid.Empty)
            return ApiError("Tenant context required.", StatusCodes.Status400BadRequest);

        var doc = await _context.StudentDocuments
            .FirstOrDefaultAsync(d => d.Id == id && d.OrganizationId == targetOrgId && !d.IsDeleted, cancellationToken);

        if (doc == null)
            return ApiNotFound("Document not found.");

        if (!CurrentUserService.IsSuperAdmin && CurrentUserService.BranchId.HasValue && CurrentUserService.RoleType == UserRoleType.Principal)
        {
            if (doc.BranchId != CurrentUserService.BranchId.Value)
                return ApiForbidden("You cannot delete documents belonging to another branch.");
        }

        doc.IsDeleted = true;
        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            AuditAction.Delete, "Documents", "StudentDocument", doc.Id.ToString(),
            $"Deleted document '{doc.Title}'",
            organizationId: targetOrgId,
            branchId: doc.BranchId,
            userId: CurrentUserService.UserId,
            userEmail: CurrentUserService.Email,
            cancellationToken: cancellationToken);

        return ApiSuccess("Document removed successfully.");
    }
}
