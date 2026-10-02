using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Api.Authorization;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.Common.Models;
using SchoolManagement.Domain.Constants;
using SchoolManagement.Domain.Entities.Academics;
using SchoolManagement.Domain.Entities.Security;
using SchoolManagement.Domain.Entities.Student;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class StudentsController : BaseApiController
{
    private readonly IApplicationDbContext _context;
    private readonly IAuditService _auditService;
    private readonly ISmsNotificationService? _smsNotificationService;

    public StudentsController(
        IApplicationDbContext context,
        IAuditService auditService,
        ISmsNotificationService? smsNotificationService = null)
    {
        _context = context;
        _auditService = auditService;
        _smsNotificationService = smsNotificationService;
    }

    [HttpGet]
    [RequirePermission(Permissions.StudentsView)]
    public async Task<IActionResult> GetStudents(
        [FromQuery] string? search,
        [FromQuery] string? grade,
        [FromQuery] string? section,
        [FromQuery] StudentStatus? status,
        [FromQuery] Guid? branchId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 100);

        Guid targetOrgId = CurrentUserService.IsSuperAdmin ? (TenantContext.OrganizationId ?? Guid.Empty) : (CurrentUserService.OrganizationId ?? Guid.Empty);

        var query = _context.Students
            .Include(s => s.Branch)
            .Include(s => s.StudentGuardians)
                .ThenInclude(sg => sg.Guardian)
            .AsQueryable();

        if (targetOrgId != Guid.Empty)
        {
            query = query.Where(s => s.OrganizationId == targetOrgId);
        }

        if (branchId.HasValue)
        {
            query = query.Where(s => s.BranchId == branchId.Value);
        }

        if (!string.IsNullOrWhiteSpace(grade))
        {
            query = query.Where(s => s.CurrentGrade.ToLower() == grade.Trim().ToLower());
        }

        if (!string.IsNullOrWhiteSpace(section))
        {
            query = query.Where(s => s.Section != null && s.Section.ToLower() == section.Trim().ToLower());
        }

        if (status.HasValue)
        {
            query = query.Where(s => s.Status == status.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            string term = search.Trim().ToLower();
            query = query.Where(s =>
                s.FirstName.ToLower().Contains(term) ||
                s.LastName.ToLower().Contains(term) ||
                s.AdmissionNumber.ToLower().Contains(term) ||
                (s.RollNumber != null && s.RollNumber.ToLower().Contains(term)));
        }

        int totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(s => s.AdmissionDate)
            .ThenBy(s => s.AdmissionNumber)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new StudentSummaryDto(
                s.Id,
                s.OrganizationId,
                s.BranchId,
                s.Branch != null ? s.Branch.Name : null,
                s.AdmissionNumber,
                s.RollNumber,
                s.FullName,
                s.CurrentGrade,
                s.Section,
                s.Gender,
                s.Status,
                s.AdmissionDate,
                s.StudentGuardians.Where(sg => sg.IsPrimaryContact).Select(sg => sg.Guardian != null ? sg.Guardian.FullName : null).FirstOrDefault(),
                s.StudentGuardians.Where(sg => sg.IsPrimaryContact).Select(sg => sg.Guardian != null ? sg.Guardian.Phone : null).FirstOrDefault()
            ))
            .ToListAsync(cancellationToken);

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
    [RequirePermission(Permissions.StudentsView)]
    public async Task<IActionResult> GetStudentById(Guid id, CancellationToken cancellationToken)
    {
        var student = await _context.Students
            .Include(s => s.Branch)
            .Include(s => s.StudentGuardians)
                .ThenInclude(sg => sg.Guardian)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (student == null)
            return ApiError("Student not found.", StatusCodes.Status404NotFound);

        if (!CurrentUserService.IsSuperAdmin && student.OrganizationId != CurrentUserService.OrganizationId)
            return ApiError("Unauthorized access to student outside your organization.", StatusCodes.Status403Forbidden);

        var guardians = student.StudentGuardians
            .Where(sg => sg.Guardian != null && !sg.Guardian.IsDeleted)
            .Select(sg => new StudentGuardianDto(
                sg.GuardianId,
                sg.Guardian!.FullName,
                sg.Relationship,
                sg.Guardian.Phone,
                sg.Guardian.Email,
                sg.IsPrimaryContact,
                sg.IsAuthorizedToPickup
            ))
            .ToList();

        var details = new StudentDetailsDto(
            student.Id,
            student.OrganizationId,
            student.BranchId,
            student.Branch?.Name,
            student.AdmissionNumber,
            student.RollNumber,
            student.FirstName,
            student.LastName,
            student.FullName,
            student.DateOfBirth,
            student.Gender,
            student.BloodGroup,
            student.NationalIdOrBForm,
            student.Status,
            student.AdmissionDate,
            student.CurrentGrade,
            student.Section,
            student.EmergencyContactName,
            student.EmergencyContactPhone,
            student.EmergencyContactRelationship,
            student.Address,
            student.City,
            student.MedicalNotes,
            student.ProfilePictureUrl,
            guardians,
            student.CreatedAtUtc
        );

        return ApiSuccess(details);
    }

    [HttpPost]
    [RequirePermission(Permissions.StudentsCreate)]
    public async Task<IActionResult> CreateStudent([FromBody] CreateStudentRequestDto request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.LastName))
            return ApiError("Student first name and last name are required.");

        if (string.IsNullOrWhiteSpace(request.CurrentGrade))
            return ApiError("Current grade / class is required.");

        Guid targetOrgId = CurrentUserService.IsSuperAdmin && TenantContext.OrganizationId.HasValue
            ? TenantContext.OrganizationId.Value
            : (CurrentUserService.OrganizationId ?? Guid.Empty);

        if (targetOrgId == Guid.Empty)
            return ApiError("Tenant context required.", StatusCodes.Status400BadRequest);

        // Branch validation
        Guid? targetBranchId = request.BranchId ?? CurrentUserService.BranchId;
        if (targetBranchId.HasValue)
        {
            bool branchExists = await _context.Branches.AnyAsync(b =>
                b.Id == targetBranchId.Value &&
                b.OrganizationId == targetOrgId &&
                !b.IsDeleted, cancellationToken);

            if (!branchExists)
                return ApiError("Specified branch does not exist or does not belong to this organization.");
        }

        // Generate Admission Number if not provided
        string admissionNumber = request.AdmissionNumber?.Trim().ToUpperInvariant() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(admissionNumber))
        {
            int studentCount = await _context.Students
                .IgnoreQueryFilters()
                .CountAsync(s => s.OrganizationId == targetOrgId, cancellationToken) + 1;
            admissionNumber = $"ADM-{DateTime.UtcNow.Year}-{studentCount:D4}";

            while (await _context.Students.IgnoreQueryFilters().AnyAsync(s => s.OrganizationId == targetOrgId && s.AdmissionNumber == admissionNumber, cancellationToken))
            {
                studentCount++;
                admissionNumber = $"ADM-{DateTime.UtcNow.Year}-{studentCount:D4}";
            }
        }

        // Uniqueness check for Admission Number (including soft-deleted records to prevent DB unique key violation)
        bool admissionExists = await _context.Students
            .IgnoreQueryFilters()
            .AnyAsync(s =>
                s.OrganizationId == targetOrgId &&
                s.AdmissionNumber.ToUpper() == admissionNumber,
                cancellationToken);

        if (admissionExists)
            return ApiError($"A student with admission number '{admissionNumber}' already exists in this organization.");

        string gradeName = string.IsNullOrWhiteSpace(request.CurrentGrade) ? "General" : request.CurrentGrade.Trim();
        var classGrade = await _context.ClassGrades
            .FirstOrDefaultAsync(cg => cg.OrganizationId == targetOrgId && cg.Name.ToLower() == gradeName.ToLower() && !cg.IsDeleted, cancellationToken);

        if (classGrade != null)
        {
            if (!classGrade.IsActive)
                return ApiError($"Cannot enroll student in inactive class/grade '{classGrade.Name}'.", StatusCodes.Status400BadRequest);

            int currentInGrade = await _context.Students
                .CountAsync(s => s.OrganizationId == targetOrgId && s.CurrentGrade.ToLower() == gradeName.ToLower() && s.Status == StudentStatus.Active && !s.IsDeleted, cancellationToken);

            if (currentInGrade >= classGrade.Capacity)
                return ApiError($"Class capacity reached. Maximum capacity for '{classGrade.Name}' is {classGrade.Capacity}. Current active students: {currentInGrade}.", StatusCodes.Status400BadRequest);
        }

        if (!string.IsNullOrWhiteSpace(request.Section) && classGrade != null)
        {
            var section = await _context.ClassSections
                .FirstOrDefaultAsync(cs => cs.OrganizationId == targetOrgId && cs.ClassGradeId == classGrade.Id && cs.Name.ToLower() == request.Section.Trim().ToLower() && !cs.IsDeleted, cancellationToken);

            if (section != null)
            {
                if (!section.IsActive)
                    return ApiError($"Cannot enroll student in inactive section '{section.Name}'.", StatusCodes.Status400BadRequest);

                int currentInSection = await _context.Students
                    .CountAsync(s => s.OrganizationId == targetOrgId && s.CurrentGrade.ToLower() == gradeName.ToLower() && s.Section != null && s.Section.ToLower() == request.Section.Trim().ToLower() && s.Status == StudentStatus.Active && !s.IsDeleted, cancellationToken);

                if (currentInSection >= section.Capacity)
                    return ApiError($"Section capacity reached. Maximum capacity for '{section.Name}' is {section.Capacity}. Current active students: {currentInSection}.", StatusCodes.Status400BadRequest);
            }
        }

        var student = new Student
        {
            Id = Guid.NewGuid(),
            OrganizationId = targetOrgId,
            BranchId = targetBranchId,
            AdmissionNumber = admissionNumber,
            RollNumber = request.RollNumber?.Trim(),
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            DateOfBirth = request.DateOfBirth ?? DateTime.UtcNow.AddYears(-10),
            Gender = request.Gender,
            BloodGroup = request.BloodGroup?.Trim(),
            NationalIdOrBForm = request.NationalIdOrBForm?.Trim(),
            AdmissionDate = request.AdmissionDate ?? DateTime.UtcNow,
            Status = StudentStatus.Active,
            CurrentGrade = string.IsNullOrWhiteSpace(request.CurrentGrade) ? "General" : request.CurrentGrade.Trim(),
            Section = request.Section?.Trim(),
            EmergencyContactName = request.EmergencyContactName?.Trim(),
            EmergencyContactPhone = request.EmergencyContactPhone?.Trim(),
            EmergencyContactRelationship = request.EmergencyContactRelationship?.Trim(),
            Address = request.Address?.Trim(),
            City = request.City?.Trim(),
            MedicalNotes = request.MedicalNotes?.Trim()
        };

        // Create initial guardian if provided
        if (request.InitialGuardian != null && !string.IsNullOrWhiteSpace(request.InitialGuardian.FirstName) && !string.IsNullOrWhiteSpace(request.InitialGuardian.Phone))
        {
            var gReq = request.InitialGuardian;
            var guardian = new Guardian
            {
                Id = Guid.NewGuid(),
                OrganizationId = targetOrgId,
                BranchId = targetBranchId,
                FirstName = gReq.FirstName.Trim(),
                LastName = !string.IsNullOrWhiteSpace(gReq.LastName) ? gReq.LastName.Trim() : request.LastName.Trim(),
                RelationshipType = gReq.RelationshipType,
                NationalId = gReq.NationalId?.Trim(),
                Email = gReq.Email?.Trim(),
                Phone = gReq.Phone.Trim(),
                AlternatePhone = gReq.AlternatePhone?.Trim(),
                Occupation = gReq.Occupation?.Trim(),
                AnnualIncome = gReq.AnnualIncome,
                Address = gReq.Address?.Trim() ?? student.Address,
                City = gReq.City?.Trim() ?? student.City,
                IsEmergencyContact = gReq.IsEmergencyContact
            };

            var studentGuardian = new StudentGuardian
            {
                Id = Guid.NewGuid(),
                OrganizationId = targetOrgId,
                BranchId = targetBranchId,
                StudentId = student.Id,
                GuardianId = guardian.Id,
                Relationship = string.IsNullOrWhiteSpace(request.GuardianRelationship) ? gReq.RelationshipType.ToString() : request.GuardianRelationship.Trim(),
                IsPrimaryContact = request.IsPrimaryGuardian,
                IsAuthorizedToPickup = true
            };

            _context.Guardians.Add(guardian);
            student.StudentGuardians.Add(studentGuardian);
        }

        _context.Students.Add(student);
        await _context.SaveChangesAsync(cancellationToken);

        // Auto-trigger SMS notification for admission
        if (_smsNotificationService != null)
        {
            await _smsNotificationService.SendAdmissionStatusChangeAsync(targetOrgId, student.Id, "Admitted", cancellationToken);
        }

        await _auditService.LogAsync(
            AuditAction.Create, "Students", "Student", student.Id.ToString(),
            $"Admitted student '{student.FullName}' ({student.AdmissionNumber}) to grade '{student.CurrentGrade}'",
            organizationId: targetOrgId,
            branchId: targetBranchId,
            userId: CurrentUserService.UserId,
            userEmail: CurrentUserService.Email,
            cancellationToken: cancellationToken);

        return ApiSuccess(new { id = student.Id, admissionNumber = student.AdmissionNumber, fullName = student.FullName }, "Student admitted successfully.");
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(Permissions.StudentsEdit)]
    public async Task<IActionResult> UpdateStudent(Guid id, [FromBody] UpdateStudentRequestDto request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.LastName))
            return ApiError("Student first name and last name are required.");

        if (string.IsNullOrWhiteSpace(request.CurrentGrade))
            return ApiError("Current grade / class is required.");

        var student = await _context.Students.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (student == null)
            return ApiError("Student not found.", StatusCodes.Status404NotFound);

        if (!CurrentUserService.IsSuperAdmin && student.OrganizationId != CurrentUserService.OrganizationId)
            return ApiError("Unauthorized access to student outside your organization.", StatusCodes.Status403Forbidden);

        if (request.BranchId.HasValue)
        {
            bool branchExists = await _context.Branches.AnyAsync(b =>
                b.Id == request.BranchId.Value &&
                b.OrganizationId == student.OrganizationId &&
                !b.IsDeleted, cancellationToken);

            if (!branchExists)
                return ApiError("Specified branch does not exist or does not belong to this organization.");

            student.BranchId = request.BranchId;
        }

        string targetGrade = request.CurrentGrade.Trim();
        if (!string.Equals(student.CurrentGrade, targetGrade, StringComparison.OrdinalIgnoreCase))
        {
            var classGrade = await _context.ClassGrades
                .FirstOrDefaultAsync(cg => cg.OrganizationId == student.OrganizationId && cg.Name.ToLower() == targetGrade.ToLower() && !cg.IsDeleted, cancellationToken);

            if (classGrade != null)
            {
                if (!classGrade.IsActive)
                    return ApiError($"Cannot assign student to inactive class/grade '{classGrade.Name}'.", StatusCodes.Status400BadRequest);

                int currentInGrade = await _context.Students
                    .CountAsync(s => s.OrganizationId == student.OrganizationId && s.CurrentGrade.ToLower() == targetGrade.ToLower() && s.Status == StudentStatus.Active && !s.IsDeleted, cancellationToken);

                if (currentInGrade >= classGrade.Capacity)
                    return ApiError($"Class capacity reached. Maximum capacity for '{classGrade.Name}' is {classGrade.Capacity}.", StatusCodes.Status400BadRequest);
            }
        }

        student.FirstName = request.FirstName.Trim();
        student.LastName = request.LastName.Trim();
        student.RollNumber = request.RollNumber?.Trim();
        student.DateOfBirth = request.DateOfBirth ?? student.DateOfBirth;
        student.Gender = request.Gender;
        student.BloodGroup = request.BloodGroup?.Trim();
        student.NationalIdOrBForm = request.NationalIdOrBForm?.Trim();
        student.Status = request.Status;
        student.CurrentGrade = targetGrade;
        student.Section = request.Section?.Trim();
        student.EmergencyContactName = request.EmergencyContactName?.Trim();
        student.EmergencyContactPhone = request.EmergencyContactPhone?.Trim();
        student.EmergencyContactRelationship = request.EmergencyContactRelationship?.Trim();
        student.Address = request.Address?.Trim();
        student.City = request.City?.Trim();
        student.MedicalNotes = request.MedicalNotes?.Trim();

        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            AuditAction.Update, "Students", "Student", student.Id.ToString(),
            $"Updated student profile for '{student.FullName}' ({student.AdmissionNumber})",
            organizationId: student.OrganizationId,
            branchId: student.BranchId,
            userId: CurrentUserService.UserId,
            userEmail: CurrentUserService.Email,
            cancellationToken: cancellationToken);

        return ApiSuccess("Student profile updated successfully.");
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(Permissions.StudentsDelete)]
    public async Task<IActionResult> DeleteStudent(Guid id, CancellationToken cancellationToken)
    {
        var student = await _context.Students.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (student == null)
            return ApiError("Student not found.", StatusCodes.Status404NotFound);

        if (!CurrentUserService.IsSuperAdmin && student.OrganizationId != CurrentUserService.OrganizationId)
            return ApiError("Unauthorized access to student outside your organization.", StatusCodes.Status403Forbidden);

        student.IsDeleted = true;
        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            AuditAction.Delete, "Students", "Student", student.Id.ToString(),
            $"Soft-deleted student record '{student.FullName}' ({student.AdmissionNumber})",
            organizationId: student.OrganizationId,
            branchId: student.BranchId,
            userId: CurrentUserService.UserId,
            userEmail: CurrentUserService.Email,
            cancellationToken: cancellationToken);

        return ApiSuccess("Student record deleted successfully.");
    }

    [HttpPost("{id:guid}/guardians")]
    [RequirePermission(Permissions.StudentsEdit)]
    public async Task<IActionResult> AssignGuardian(Guid id, [FromBody] AssignGuardianRequestDto request, CancellationToken cancellationToken)
    {
        var student = await _context.Students.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (student == null)
            return ApiError("Student not found.", StatusCodes.Status404NotFound);

        if (!CurrentUserService.IsSuperAdmin && student.OrganizationId != CurrentUserService.OrganizationId)
            return ApiError("Unauthorized access to student outside your organization.", StatusCodes.Status403Forbidden);

        var guardian = await _context.Guardians.FirstOrDefaultAsync(g => g.Id == request.GuardianId && !g.IsDeleted, cancellationToken);
        if (guardian == null)
            return ApiError("Guardian not found.", StatusCodes.Status404NotFound);

        if (guardian.OrganizationId != student.OrganizationId)
            return ApiError("Guardian does not belong to the same organization.", StatusCodes.Status400BadRequest);

        bool linkExists = await _context.StudentGuardians.AnyAsync(sg =>
            sg.StudentId == id && sg.GuardianId == request.GuardianId && !sg.IsDeleted, cancellationToken);

        if (linkExists)
            return ApiError("This guardian is already linked to the student.");

        // If this is set as primary contact, demote previous primary contacts
        if (request.IsPrimaryContact)
        {
            var existingLinks = await _context.StudentGuardians
                .Where(sg => sg.StudentId == id && !sg.IsDeleted)
                .ToListAsync(cancellationToken);

            foreach (var link in existingLinks)
            {
                link.IsPrimaryContact = false;
            }
        }

        var studentGuardian = new StudentGuardian
        {
            Id = Guid.NewGuid(),
            OrganizationId = student.OrganizationId,
            BranchId = student.BranchId,
            StudentId = student.Id,
            GuardianId = guardian.Id,
            Relationship = request.Relationship.Trim(),
            IsPrimaryContact = request.IsPrimaryContact,
            IsAuthorizedToPickup = request.IsAuthorizedToPickup
        };

        _context.StudentGuardians.Add(studentGuardian);
        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            AuditAction.Create, "Students", "StudentGuardian", studentGuardian.Id.ToString(),
            $"Linked guardian '{guardian.FullName}' ({request.Relationship}) to student '{student.FullName}'",
            organizationId: student.OrganizationId,
            branchId: student.BranchId,
            userId: CurrentUserService.UserId,
            userEmail: CurrentUserService.Email,
            cancellationToken: cancellationToken);

        return ApiSuccess("Guardian linked successfully.");
    }

    [HttpDelete("{id:guid}/guardians/{guardianId:guid}")]
    [RequirePermission(Permissions.StudentsEdit)]
    public async Task<IActionResult> UnlinkGuardian(Guid id, Guid guardianId, CancellationToken cancellationToken)
    {
        var link = await _context.StudentGuardians
            .FirstOrDefaultAsync(sg => sg.StudentId == id && sg.GuardianId == guardianId && !sg.IsDeleted, cancellationToken);

        if (link == null)
            return ApiError("Guardian link not found.", StatusCodes.Status404NotFound);

        if (!CurrentUserService.IsSuperAdmin && link.OrganizationId != CurrentUserService.OrganizationId)
            return ApiError("Unauthorized access.", StatusCodes.Status403Forbidden);

        link.IsDeleted = true;
        await _context.SaveChangesAsync(cancellationToken);

        return ApiSuccess("Guardian unlinked successfully.");
    }

    [HttpGet("{id:guid}/documents")]
    [RequirePermission(Permissions.StudentsView)]
    public async Task<IActionResult> GetStudentDocuments(Guid id, CancellationToken cancellationToken)
    {
        var student = await _context.Students.FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted, cancellationToken);
        if (student == null) return ApiNotFound("Student not found.");

        if (!CurrentUserService.IsSuperAdmin && student.OrganizationId != CurrentUserService.OrganizationId)
            return ApiForbidden("Unauthorized cross-organization access.");

        var docs = await _context.StudentDocuments
            .Where(d => d.StudentId == id && !d.IsDeleted)
            .OrderByDescending(d => d.CreatedAtUtc)
            .Select(d => new StudentDocumentDto(
                d.Id,
                d.Title,
                d.DocumentType,
                d.DocumentType.ToString(),
                d.FileName,
                d.FileUrl,
                d.FileSizeBytes,
                d.Description,
                d.CreatedAtUtc
            ))
            .ToListAsync(cancellationToken);

        return ApiSuccess(docs);
    }

    [HttpPost("{id:guid}/documents")]
    [RequirePermission(Permissions.StudentsEdit)]
    public async Task<IActionResult> AddStudentDocument(Guid id, [FromBody] UploadStudentDocumentRequest request, CancellationToken cancellationToken)
    {
        var student = await _context.Students.FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted, cancellationToken);
        if (student == null) return ApiNotFound("Student not found.");

        if (!CurrentUserService.IsSuperAdmin && student.OrganizationId != CurrentUserService.OrganizationId)
            return ApiForbidden("Unauthorized cross-organization access.");

        var doc = new StudentDocument
        {
            Id = Guid.NewGuid(),
            OrganizationId = student.OrganizationId,
            BranchId = student.BranchId,
            StudentId = student.Id,
            Title = request.Title ?? "Document",
            DocumentType = request.DocumentType,
            FileUrl = request.FileUrl ?? $"/documents/{Guid.NewGuid()}.pdf",
            FileName = request.FileName ?? "document.pdf",
            FileSizeBytes = request.FileSizeBytes > 0 ? request.FileSizeBytes : 1024,
            Description = request.Description,
            UploadedByUserId = CurrentUserService.UserId
        };

        _context.StudentDocuments.Add(doc);
        await _context.SaveChangesAsync(cancellationToken);

        return ApiSuccess(doc.Id, "Student document uploaded successfully.");
    }

    [HttpPost("{id:guid}/portal-access")]
    [RequirePermission(Permissions.StudentsEdit)]
    public async Task<IActionResult> CreatePortalAccess(
        Guid id,
        [FromBody] CreateStudentPortalAccessRequest request,
        [FromServices] IPasswordHasher passwordHasher,
        CancellationToken cancellationToken)
    {
        var student = await _context.Students
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted, cancellationToken);
        if (student == null) return ApiNotFound("Student not found.");

        if (!CurrentUserService.IsSuperAdmin && student.OrganizationId != CurrentUserService.OrganizationId)
            return ApiForbidden("Unauthorized cross-organization access.");

        string email = string.IsNullOrWhiteSpace(request.Email) ? $"student.{student.AdmissionNumber.ToLowerInvariant().Replace('-', '.')}@school.local" : request.Email.Trim();
        string username = string.IsNullOrWhiteSpace(request.Username) ? student.AdmissionNumber.ToLowerInvariant() : request.Username.Trim();
        string password = string.IsNullOrWhiteSpace(request.Password) ? "Student@2026!Pak" : request.Password;

        // Check if user account already linked
        if (student.UserId.HasValue && student.User != null)
        {
            var (h, s) = passwordHasher.HashPassword(password);
            student.User.PasswordHash = h;
            student.User.PasswordSalt = s;
            student.User.Email = email;
            student.User.Username = username;
            student.User.NormalizedEmail = email.ToUpperInvariant();
            await _context.SaveChangesAsync(cancellationToken);
            return ApiSuccess(new { userId = student.UserId.Value, username, email }, "Student portal credentials updated successfully.");
        }

        var (hash, salt) = passwordHasher.HashPassword(password);
        var user = new User
        {
            Id = Guid.NewGuid(),
            OrganizationId = student.OrganizationId,
            BranchId = student.BranchId,
            Username = username,
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            FirstName = student.FirstName,
            LastName = student.LastName,
            PasswordHash = hash,
            PasswordSalt = salt,
            RoleType = UserRoleType.Student,
            IsActive = true
        };

        var studentRole = await _context.Roles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.IsSystemRole && r.Name == "Student", cancellationToken);

        if (studentRole != null)
        {
            user.UserRoles.Add(new UserRole
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                RoleId = studentRole.Id
            });
        }

        student.UserId = user.Id;
        _context.Users.Add(user);
        await _context.SaveChangesAsync(cancellationToken);

        return ApiSuccess(new { userId = user.Id, username, email }, "Student portal account created successfully.");
    }

    [HttpPost("{id:guid}/transfer-branch")]
    [RequirePermission(Permissions.StudentsEdit)]
    public async Task<IActionResult> TransferBranch(Guid id, [FromBody] TransferBranchRequestDto request, CancellationToken cancellationToken)
    {
        var student = await _context.Students
            .Include(s => s.Branch)
            .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted, cancellationToken);

        if (student == null)
            return ApiError("Student not found.", StatusCodes.Status404NotFound);

        if (!CurrentUserService.IsSuperAdmin && student.OrganizationId != CurrentUserService.OrganizationId)
            return ApiError("Unauthorized access to student outside your organization.", StatusCodes.Status403Forbidden);

        var targetBranch = await _context.Branches
            .FirstOrDefaultAsync(b => b.Id == request.TargetBranchId && b.OrganizationId == student.OrganizationId && !b.IsDeleted, cancellationToken);

        if (targetBranch == null)
            return ApiError("Target branch not found in this organization.", StatusCodes.Status404NotFound);

        if (!targetBranch.IsActive)
            return ApiError("Cannot transfer student to a deactivated campus branch.", StatusCodes.Status400BadRequest);

        var oldBranchName = student.Branch?.Name ?? "Main Campus";
        student.BranchId = request.TargetBranchId;
        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            AuditAction.Update, "Students", "BranchTransfer", student.Id.ToString(),
            $"Transferred student '{student.FullName}' ({student.AdmissionNumber}) from '{oldBranchName}' to '{targetBranch.Name}'. Reason: {request.Reason ?? "Not specified"}",
            organizationId: student.OrganizationId,
            branchId: targetBranch.Id,
            userId: CurrentUserService.UserId,
            userEmail: CurrentUserService.Email,
            cancellationToken: cancellationToken);

        return ApiSuccess(new { studentId = student.Id, newBranchId = targetBranch.Id, newBranchName = targetBranch.Name },
            $"Student transferred to branch '{targetBranch.Name}' successfully.");
    }

    [HttpPost("{id:guid}/transfer-class")]
    [RequirePermission(Permissions.StudentsEdit)]
    public async Task<IActionResult> TransferClass(Guid id, [FromBody] TransferClassRequestDto request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.TargetGrade))
            return ApiError("Target grade/class is required.");

        var student = await _context.Students.FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted, cancellationToken);
        if (student == null)
            return ApiError("Student not found.", StatusCodes.Status404NotFound);

        if (!CurrentUserService.IsSuperAdmin && student.OrganizationId != CurrentUserService.OrganizationId)
            return ApiError("Unauthorized access to student outside your organization.", StatusCodes.Status403Forbidden);

        string normGrade = request.TargetGrade.Trim();
        var classGrade = await _context.ClassGrades
            .FirstOrDefaultAsync(cg => cg.OrganizationId == student.OrganizationId && cg.Name.ToLower() == normGrade.ToLower() && !cg.IsDeleted, cancellationToken);

        if (classGrade == null)
            return ApiError($"Target grade/class '{request.TargetGrade}' does not exist.", StatusCodes.Status404NotFound);

        if (!classGrade.IsActive)
            return ApiError($"Cannot transfer student to inactive class/grade '{classGrade.Name}'.", StatusCodes.Status400BadRequest);

        // Check grade capacity
        int currentInGrade = await _context.Students
            .CountAsync(s => s.OrganizationId == student.OrganizationId && s.CurrentGrade.ToLower() == normGrade.ToLower() && s.Status == StudentStatus.Active && !s.IsDeleted, cancellationToken);

        if (currentInGrade >= classGrade.Capacity && !string.Equals(student.CurrentGrade, normGrade, StringComparison.OrdinalIgnoreCase))
            return ApiError($"Class capacity reached. Maximum capacity for '{classGrade.Name}' is {classGrade.Capacity}.", StatusCodes.Status400BadRequest);

        // Check section capacity if section specified
        string? normSection = request.TargetSection?.Trim();
        if (!string.IsNullOrWhiteSpace(normSection))
        {
            var section = await _context.ClassSections
                .FirstOrDefaultAsync(cs => cs.OrganizationId == student.OrganizationId && cs.ClassGradeId == classGrade.Id && cs.Name.ToLower() == normSection.ToLower() && !cs.IsDeleted, cancellationToken);

            if (section != null)
            {
                if (!section.IsActive)
                    return ApiError($"Cannot transfer student to inactive section '{section.Name}'.", StatusCodes.Status400BadRequest);

                int currentInSection = await _context.Students
                    .CountAsync(s => s.OrganizationId == student.OrganizationId && s.CurrentGrade.ToLower() == normGrade.ToLower() && s.Section != null && s.Section.ToLower() == normSection.ToLower() && s.Status == StudentStatus.Active && !s.IsDeleted, cancellationToken);

                if (currentInSection >= section.Capacity)
                    return ApiError($"Section capacity reached. Maximum capacity for '{section.Name}' is {section.Capacity}.", StatusCodes.Status400BadRequest);
            }
        }

        var oldGrade = student.CurrentGrade;
        var oldSection = student.Section;

        student.CurrentGrade = classGrade.Name;
        student.Section = normSection;

        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            AuditAction.Update, "Students", "ClassTransfer", student.Id.ToString(),
            $"Transferred student '{student.FullName}' ({student.AdmissionNumber}) from '{oldGrade} {oldSection}' to '{student.CurrentGrade} {student.Section}'. Reason: {request.Reason ?? "Administrative adjustment"}",
            organizationId: student.OrganizationId,
            branchId: student.BranchId,
            userId: CurrentUserService.UserId,
            userEmail: CurrentUserService.Email,
            cancellationToken: cancellationToken);

        return ApiSuccess(new { studentId = student.Id, newGrade = student.CurrentGrade, newSection = student.Section },
            $"Student class transferred successfully to '{student.CurrentGrade} {student.Section}'.");
    }

    [HttpPost("{id:guid}/status")]
    [RequirePermission(Permissions.StudentsEdit)]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateStudentStatusRequestDto request, CancellationToken cancellationToken)
    {
        var student = await _context.Students.FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted, cancellationToken);
        if (student == null)
            return ApiError("Student not found.", StatusCodes.Status404NotFound);

        if (!CurrentUserService.IsSuperAdmin && student.OrganizationId != CurrentUserService.OrganizationId)
            return ApiError("Unauthorized access to student outside your organization.", StatusCodes.Status403Forbidden);

        if ((request.Status == StudentStatus.Graduated || request.Status == StudentStatus.Withdrawn) && !request.ForceBypassClearance)
        {
            var unpaidFeeTotal = await _context.FeeInvoices
                .Where(f => f.StudentId == id && f.TotalAmount > f.PaidAmount && !f.IsDeleted)
                .SumAsync(f => f.TotalAmount - f.PaidAmount, cancellationToken);

            if (unpaidFeeTotal > 0)
                return ApiError($"Cannot {request.Status} student: Clearance blocked due to outstanding fee balance ({unpaidFeeTotal:C}). Settle dues or provide explicit administrative bypass.", StatusCodes.Status400BadRequest);

            int unreturnedBooks = await _context.BookIssues
                .CountAsync(b => b.StudentId == id && b.Status == BookIssueStatus.Issued && !b.IsDeleted, cancellationToken);

            if (unreturnedBooks > 0)
                return ApiError($"Cannot {request.Status} student: Clearance blocked due to {unreturnedBooks} unreturned library book(s). Return books or provide explicit administrative bypass.", StatusCodes.Status400BadRequest);

            var activeTransports = await _context.TransportAllocations
                .Where(t => t.StudentId == id && t.IsActive && !t.IsDeleted)
                .ToListAsync(cancellationToken);

            foreach (var t in activeTransports)
            {
                t.IsActive = false;
            }
        }

        var prevStatus = student.Status;
        student.Status = request.Status;

        await _context.SaveChangesAsync(cancellationToken);

        // Auto-trigger SMS notification for status change
        if (_smsNotificationService != null)
        {
            await _smsNotificationService.SendAdmissionStatusChangeAsync(student.OrganizationId, student.Id, student.Status.ToString(), cancellationToken);
        }

        await _auditService.LogAsync(
            AuditAction.Update, "Students", "StudentStatus", student.Id.ToString(),
            $"Student '{student.FullName}' ({student.AdmissionNumber}) status updated from '{prevStatus}' to '{student.Status}'. Remarks: {request.Remarks ?? "None"}",
            organizationId: student.OrganizationId,
            branchId: student.BranchId,
            userId: CurrentUserService.UserId,
            userEmail: CurrentUserService.Email,
            cancellationToken: cancellationToken);

        return ApiSuccess(new { studentId = student.Id, previousStatus = prevStatus.ToString(), currentStatus = student.Status.ToString() },
            $"Student status changed to '{student.Status}'.");
    }

    // =========================================================================
    // ADMISSION APPLICATION & ENROLLMENT WORKFLOW
    // =========================================================================

    [HttpPost("applications")]
    [AllowAnonymous]
    public async Task<IActionResult> SubmitApplication([FromBody] SubmitAdmissionApplicationRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.LastName))
            return ApiError("Applicant first name and last name are required.");

        if (string.IsNullOrWhiteSpace(request.DesiredGrade))
            return ApiError("Desired grade/class is required.");

        Guid targetOrgId = CurrentUserService.IsSuperAdmin && TenantContext.OrganizationId.HasValue
            ? TenantContext.OrganizationId.Value
            : (CurrentUserService.OrganizationId ?? TenantContext.OrganizationId ?? Guid.Empty);

        if (targetOrgId == Guid.Empty)
        {
            var defaultOrg = await _context.Organizations.FirstOrDefaultAsync(o => !o.IsDeleted, cancellationToken);
            if (defaultOrg != null) targetOrgId = defaultOrg.Id;
            else return ApiError("Organization context required.", StatusCodes.Status400BadRequest);
        }

        int count = await _context.AdmissionApplications.CountAsync(a => a.OrganizationId == targetOrgId, cancellationToken) + 1;
        string appNum = $"APP-{DateTime.UtcNow.Year}-{count:D4}";

        var application = new AdmissionApplication
        {
            Id = Guid.NewGuid(),
            OrganizationId = targetOrgId,
            BranchId = request.BranchId ?? CurrentUserService.BranchId,
            ApplicationNumber = appNum,
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            DateOfBirth = request.DateOfBirth,
            Gender = request.Gender,
            DesiredGrade = request.DesiredGrade.Trim(),
            GuardianName = request.GuardianName?.Trim(),
            GuardianPhone = request.GuardianPhone?.Trim(),
            GuardianEmail = request.GuardianEmail?.Trim(),
            GuardianRelationship = request.GuardianRelationship?.Trim(),
            Address = request.Address?.Trim(),
            Status = ApplicationStatus.Pending
        };

        _context.AdmissionApplications.Add(application);
        await _context.SaveChangesAsync(cancellationToken);

        return ApiSuccess(new { applicationId = application.Id, applicationNumber = application.ApplicationNumber },
            $"Admission application '{application.ApplicationNumber}' submitted successfully.");
    }

    [HttpGet("applications")]
    [RequirePermission(Permissions.StudentsView)]
    public async Task<IActionResult> GetApplications(
        [FromQuery] ApplicationStatus? status,
        [FromQuery] string? grade,
        [FromQuery] string? search,
        CancellationToken cancellationToken)
    {
        Guid targetOrgId = CurrentUserService.IsSuperAdmin && TenantContext.OrganizationId.HasValue
            ? TenantContext.OrganizationId.Value
            : (CurrentUserService.OrganizationId ?? Guid.Empty);

        var query = _context.AdmissionApplications
            .Include(a => a.Branch)
            .Where(a => a.OrganizationId == targetOrgId && !a.IsDeleted);

        if (status.HasValue) query = query.Where(a => a.Status == status.Value);
        if (!string.IsNullOrWhiteSpace(grade)) query = query.Where(a => a.DesiredGrade.ToLower() == grade.Trim().ToLower());
        if (!string.IsNullOrWhiteSpace(search))
        {
            string term = search.Trim().ToLower();
            query = query.Where(a => a.FirstName.ToLower().Contains(term) || a.LastName.ToLower().Contains(term) || a.ApplicationNumber.ToLower().Contains(term));
        }

        var list = await query
            .OrderByDescending(a => a.CreatedAtUtc)
            .Select(a => new AdmissionApplicationDto(
                a.Id,
                a.OrganizationId,
                a.BranchId,
                a.Branch != null ? a.Branch.Name : null,
                a.ApplicationNumber,
                a.FirstName,
                a.LastName,
                a.FirstName + " " + a.LastName,
                a.DateOfBirth,
                a.Gender,
                a.DesiredGrade,
                a.GuardianName,
                a.GuardianPhone,
                a.GuardianEmail,
                a.GuardianRelationship,
                a.Address,
                a.Status,
                a.Status.ToString(),
                a.ReviewNotes,
                a.RejectionReason,
                a.EnrolledStudentId,
                a.DecisionDateUtc,
                a.CreatedAtUtc
            ))
            .ToListAsync(cancellationToken);

        return ApiSuccess(list);
    }

    [HttpGet("applications/{id:guid}")]
    [RequirePermission(Permissions.StudentsView)]
    public async Task<IActionResult> GetApplicationById(Guid id, CancellationToken cancellationToken)
    {
        Guid targetOrgId = CurrentUserService.IsSuperAdmin && TenantContext.OrganizationId.HasValue
            ? TenantContext.OrganizationId.Value
            : (CurrentUserService.OrganizationId ?? Guid.Empty);

        var a = await _context.AdmissionApplications
            .Include(app => app.Branch)
            .FirstOrDefaultAsync(app => app.Id == id && app.OrganizationId == targetOrgId && !app.IsDeleted, cancellationToken);

        if (a == null) return ApiNotFound("Admission application not found.");

        var dto = new AdmissionApplicationDto(
            a.Id, a.OrganizationId, a.BranchId, a.Branch?.Name, a.ApplicationNumber,
            a.FirstName, a.LastName, a.FirstName + " " + a.LastName, a.DateOfBirth, a.Gender,
            a.DesiredGrade, a.GuardianName, a.GuardianPhone, a.GuardianEmail, a.GuardianRelationship,
            a.Address, a.Status, a.Status.ToString(), a.ReviewNotes, a.RejectionReason,
            a.EnrolledStudentId, a.DecisionDateUtc, a.CreatedAtUtc
        );

        return ApiSuccess(dto);
    }

    [HttpPost("applications/{id:guid}/review")]
    [RequirePermission(Permissions.StudentsEdit)]
    public async Task<IActionResult> ReviewApplication(Guid id, [FromBody] ReviewApplicationRequest request, CancellationToken cancellationToken)
    {
        Guid targetOrgId = CurrentUserService.IsSuperAdmin && TenantContext.OrganizationId.HasValue
            ? TenantContext.OrganizationId.Value
            : (CurrentUserService.OrganizationId ?? Guid.Empty);

        var app = await _context.AdmissionApplications
            .FirstOrDefaultAsync(a => a.Id == id && a.OrganizationId == targetOrgId && !a.IsDeleted, cancellationToken);

        if (app == null) return ApiNotFound("Admission application not found.");

        app.Status = ApplicationStatus.UnderReview;
        app.ReviewNotes = request.ReviewNotes?.Trim();
        app.ReviewedByUserId = CurrentUserService.UserId;

        await _context.SaveChangesAsync(cancellationToken);
        return ApiSuccess("Application status updated to Under Review.");
    }

    [HttpPost("applications/{id:guid}/reject")]
    [RequirePermission(Permissions.StudentsEdit)]
    public async Task<IActionResult> RejectApplication(Guid id, [FromBody] RejectApplicationRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RejectionReason))
            return ApiError("A valid rejection reason is required.");

        Guid targetOrgId = CurrentUserService.IsSuperAdmin && TenantContext.OrganizationId.HasValue
            ? TenantContext.OrganizationId.Value
            : (CurrentUserService.OrganizationId ?? Guid.Empty);

        var app = await _context.AdmissionApplications
            .FirstOrDefaultAsync(a => a.Id == id && a.OrganizationId == targetOrgId && !a.IsDeleted, cancellationToken);

        if (app == null) return ApiNotFound("Admission application not found.");

        app.Status = ApplicationStatus.Rejected;
        app.RejectionReason = request.RejectionReason.Trim();
        app.DecisionDateUtc = DateTime.UtcNow;
        app.ReviewedByUserId = CurrentUserService.UserId;

        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            AuditAction.Update, "Students", "AdmissionApplicationRejected", app.Id.ToString(),
            $"Rejected admission application '{app.ApplicationNumber}' for '{app.FirstName} {app.LastName}'. Reason: {app.RejectionReason}",
            organizationId: app.OrganizationId,
            branchId: app.BranchId,
            userId: CurrentUserService.UserId,
            userEmail: CurrentUserService.Email,
            cancellationToken: cancellationToken);

        return ApiSuccess("Application rejected.");
    }

    [HttpPost("applications/{id:guid}/approve")]
    [RequirePermission(Permissions.StudentsCreate)]
    public async Task<IActionResult> ApproveAndEnrollApplication(Guid id, [FromBody] ApproveAndEnrollApplicationRequest request, CancellationToken cancellationToken)
    {
        Guid targetOrgId = CurrentUserService.IsSuperAdmin && TenantContext.OrganizationId.HasValue
            ? TenantContext.OrganizationId.Value
            : (CurrentUserService.OrganizationId ?? Guid.Empty);

        var app = await _context.AdmissionApplications
            .FirstOrDefaultAsync(a => a.Id == id && a.OrganizationId == targetOrgId && !a.IsDeleted, cancellationToken);

        if (app == null) return ApiNotFound("Admission application not found.");

        if (app.Status == ApplicationStatus.Enrolled)
            return ApiError("This applicant has already been enrolled as a student.");

        // Check grade capacity
        string gradeName = app.DesiredGrade.Trim();
        var classGrade = await _context.ClassGrades
            .FirstOrDefaultAsync(cg => cg.OrganizationId == targetOrgId && cg.Name.ToLower() == gradeName.ToLower() && !cg.IsDeleted, cancellationToken);

        if (classGrade != null)
        {
            if (!classGrade.IsActive)
                return ApiError($"Cannot enroll student in inactive class/grade '{classGrade.Name}'.", StatusCodes.Status400BadRequest);

            int currentInGrade = await _context.Students
                .CountAsync(s => s.OrganizationId == targetOrgId && s.CurrentGrade.ToLower() == gradeName.ToLower() && s.Status == StudentStatus.Active && !s.IsDeleted, cancellationToken);

            if (currentInGrade >= classGrade.Capacity)
                return ApiError($"Class capacity reached. Maximum capacity for '{classGrade.Name}' is {classGrade.Capacity}.", StatusCodes.Status400BadRequest);
        }

        int studentCount = await _context.Students
            .IgnoreQueryFilters()
            .CountAsync(s => s.OrganizationId == targetOrgId, cancellationToken) + 1;
        string admissionNumber = $"ADM-{DateTime.UtcNow.Year}-{studentCount:D4}";

        var student = new Student
        {
            Id = Guid.NewGuid(),
            OrganizationId = targetOrgId,
            BranchId = app.BranchId,
            AdmissionNumber = admissionNumber,
            FirstName = app.FirstName,
            LastName = app.LastName,
            DateOfBirth = app.DateOfBirth,
            Gender = app.Gender,
            CurrentGrade = app.DesiredGrade,
            Section = request.Section?.Trim(),
            Address = app.Address,
            EmergencyContactName = app.GuardianName,
            EmergencyContactPhone = app.GuardianPhone,
            AdmissionDate = DateTime.UtcNow,
            Status = StudentStatus.Active
        };

        _context.Students.Add(student);

        // Link guardian if provided
        if (!string.IsNullOrWhiteSpace(app.GuardianName) && !string.IsNullOrWhiteSpace(app.GuardianPhone))
        {
            var guardian = await _context.Guardians
                .FirstOrDefaultAsync(g => g.OrganizationId == targetOrgId && g.Phone == app.GuardianPhone.Trim() && !g.IsDeleted, cancellationToken);

            if (guardian == null)
            {
                var names = app.GuardianName.Trim().Split(' ', 2);
                guardian = new Guardian
                {
                    Id = Guid.NewGuid(),
                    OrganizationId = targetOrgId,
                    FirstName = names[0],
                    LastName = names.Length > 1 ? names[1] : "Guardian",
                    Phone = app.GuardianPhone.Trim(),
                    Email = app.GuardianEmail?.Trim(),
                    Address = app.Address
                };
                _context.Guardians.Add(guardian);
            }

            var link = new StudentGuardian
            {
                Id = Guid.NewGuid(),
                OrganizationId = targetOrgId,
                StudentId = student.Id,
                GuardianId = guardian.Id,
                Relationship = app.GuardianRelationship ?? "Parent",
                IsPrimaryContact = true,
                IsAuthorizedToPickup = true
            };
            _context.StudentGuardians.Add(link);
        }

        // Generate initial tuition/admission fee invoice if requested
        if (request.InitialTuitionFee.HasValue && request.InitialTuitionFee.Value > 0)
        {
            var invoice = new FeeInvoice
            {
                Id = Guid.NewGuid(),
                OrganizationId = targetOrgId,
                BranchId = app.BranchId,
                StudentId = student.Id,
                InvoiceNumber = $"INV-{DateTime.UtcNow:yyyyMM}-{Guid.NewGuid().ToString()[..6].ToUpper()}",
                Title = "Admission & First Term Tuition Fee",
                IssueDate = DateTime.UtcNow.Date,
                DueDate = (request.FeeDueDate ?? DateTime.UtcNow.AddDays(15)).Date,
                TotalAmount = request.InitialTuitionFee.Value,
                PaidAmount = 0m,
                Status = FeeStatus.Unpaid
            };
            _context.FeeInvoices.Add(invoice);
        }

        app.Status = ApplicationStatus.Enrolled;
        app.EnrolledStudentId = student.Id;
        app.DecisionDateUtc = DateTime.UtcNow;
        app.ReviewedByUserId = CurrentUserService.UserId;

        await _context.SaveChangesAsync(cancellationToken);

        // Auto-trigger SMS notification for enrollment approval
        if (_smsNotificationService != null)
        {
            await _smsNotificationService.SendAdmissionStatusChangeAsync(targetOrgId, student.Id, "Approved & Enrolled", cancellationToken);
        }

        await _auditService.LogAsync(
            AuditAction.Create, "Students", "AdmissionEnrollment", student.Id.ToString(),
            $"Approved application '{app.ApplicationNumber}' and enrolled student '{student.FullName}' ({student.AdmissionNumber}) into '{student.CurrentGrade}'",
            organizationId: targetOrgId,
            branchId: student.BranchId,
            userId: CurrentUserService.UserId,
            userEmail: CurrentUserService.Email,
            cancellationToken: cancellationToken);

        return ApiSuccess(new { studentId = student.Id, admissionNumber = student.AdmissionNumber, grade = student.CurrentGrade, section = student.Section },
            $"Applicant successfully enrolled as student '{student.AdmissionNumber}'.");
    }
}
