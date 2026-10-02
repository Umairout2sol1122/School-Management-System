using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Api.Authorization;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.Common.Models;
using SchoolManagement.Domain.Constants;
using SchoolManagement.Domain.Entities.Student;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class GuardiansController : BaseApiController
{
    private readonly IApplicationDbContext _context;
    private readonly IAuditService _auditService;

    public GuardiansController(IApplicationDbContext context, IAuditService auditService)
    {
        _context = context;
        _auditService = auditService;
    }

    [HttpGet]
    [RequirePermission(Permissions.ParentsView)]
    public async Task<IActionResult> GetGuardians(
        [FromQuery] string? search,
        [FromQuery] Guid? branchId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 100);

        Guid targetOrgId = CurrentUserService.IsSuperAdmin ? (TenantContext.OrganizationId ?? Guid.Empty) : (CurrentUserService.OrganizationId ?? Guid.Empty);

        var query = _context.Guardians
            .Include(g => g.StudentGuardians)
                .ThenInclude(sg => sg.Student)
            .AsQueryable();

        if (targetOrgId != Guid.Empty)
        {
            query = query.Where(g => g.OrganizationId == targetOrgId);
        }

        if (branchId.HasValue)
        {
            query = query.Where(g => g.BranchId == branchId.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            string term = search.Trim().ToLower();
            query = query.Where(g =>
                g.FirstName.ToLower().Contains(term) ||
                g.LastName.ToLower().Contains(term) ||
                g.Phone.ToLower().Contains(term) ||
                (g.NationalId != null && g.NationalId.ToLower().Contains(term)) ||
                (g.Email != null && g.Email.ToLower().Contains(term)));
        }

        int totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(g => g.LastName)
            .ThenBy(g => g.FirstName)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(g => new GuardianSummaryDto(
                g.Id,
                g.OrganizationId,
                g.FullName,
                g.RelationshipType,
                g.Phone,
                g.Email,
                g.Occupation,
                g.StudentGuardians.Count(sg => !sg.IsDeleted && sg.Student != null && !sg.Student.IsDeleted)
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
    [RequirePermission(Permissions.ParentsView)]
    public async Task<IActionResult> GetGuardianById(Guid id, CancellationToken cancellationToken)
    {
        var guardian = await _context.Guardians
            .Include(g => g.StudentGuardians)
                .ThenInclude(sg => sg.Student)
            .FirstOrDefaultAsync(g => g.Id == id, cancellationToken);

        if (guardian == null)
            return ApiError("Guardian not found.", StatusCodes.Status404NotFound);

        if (!CurrentUserService.IsSuperAdmin && guardian.OrganizationId != CurrentUserService.OrganizationId)
            return ApiError("Unauthorized access to guardian outside your organization.", StatusCodes.Status403Forbidden);

        var linkedStudents = guardian.StudentGuardians
            .Where(sg => !sg.IsDeleted && sg.Student != null && !sg.Student.IsDeleted)
            .Select(sg => new LinkedStudentDto(
                sg.StudentId,
                sg.Student!.FullName,
                sg.Student.AdmissionNumber,
                sg.Student.CurrentGrade,
                sg.Relationship,
                sg.IsPrimaryContact
            ))
            .ToList();

        var details = new GuardianDetailsDto(
            guardian.Id,
            guardian.OrganizationId,
            guardian.BranchId,
            guardian.FirstName,
            guardian.LastName,
            guardian.FullName,
            guardian.RelationshipType,
            guardian.NationalId,
            guardian.Email,
            guardian.Phone,
            guardian.AlternatePhone,
            guardian.Occupation,
            guardian.AnnualIncome,
            guardian.Address,
            guardian.City,
            guardian.IsEmergencyContact,
            linkedStudents,
            guardian.CreatedAtUtc
        );

        return ApiSuccess(details);
    }

    [HttpPost]
    [RequirePermission(Permissions.ParentsCreate)]
    public async Task<IActionResult> CreateGuardian([FromBody] CreateGuardianRequestDto request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.LastName))
            return ApiError("Guardian first name and last name are required.");

        if (string.IsNullOrWhiteSpace(request.Phone))
            return ApiError("Phone number is required.");

        Guid targetOrgId = CurrentUserService.IsSuperAdmin && TenantContext.OrganizationId.HasValue
            ? TenantContext.OrganizationId.Value
            : (CurrentUserService.OrganizationId ?? Guid.Empty);

        if (targetOrgId == Guid.Empty)
            return ApiError("Tenant context required.", StatusCodes.Status400BadRequest);

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

        var guardian = new Guardian
        {
            Id = Guid.NewGuid(),
            OrganizationId = targetOrgId,
            BranchId = targetBranchId,
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            RelationshipType = request.RelationshipType,
            NationalId = request.NationalId?.Trim(),
            Email = request.Email?.Trim(),
            Phone = request.Phone.Trim(),
            AlternatePhone = request.AlternatePhone?.Trim(),
            Occupation = request.Occupation?.Trim(),
            AnnualIncome = request.AnnualIncome,
            Address = request.Address?.Trim(),
            City = request.City?.Trim(),
            IsEmergencyContact = request.IsEmergencyContact
        };

        _context.Guardians.Add(guardian);
        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            AuditAction.Create, "Guardians", "Guardian", guardian.Id.ToString(),
            $"Created guardian profile for '{guardian.FullName}' ({guardian.RelationshipType})",
            organizationId: targetOrgId,
            branchId: targetBranchId,
            userId: CurrentUserService.UserId,
            userEmail: CurrentUserService.Email,
            cancellationToken: cancellationToken);

        return ApiSuccess(new { id = guardian.Id, fullName = guardian.FullName, phone = guardian.Phone }, "Guardian profile created successfully.");
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(Permissions.ParentsEdit)]
    public async Task<IActionResult> UpdateGuardian(Guid id, [FromBody] UpdateGuardianRequestDto request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.LastName))
            return ApiError("Guardian first name and last name are required.");

        if (string.IsNullOrWhiteSpace(request.Phone))
            return ApiError("Phone number is required.");

        var guardian = await _context.Guardians.FirstOrDefaultAsync(g => g.Id == id, cancellationToken);
        if (guardian == null)
            return ApiError("Guardian not found.", StatusCodes.Status404NotFound);

        if (!CurrentUserService.IsSuperAdmin && guardian.OrganizationId != CurrentUserService.OrganizationId)
            return ApiError("Unauthorized access to guardian outside your organization.", StatusCodes.Status403Forbidden);

        guardian.FirstName = request.FirstName.Trim();
        guardian.LastName = request.LastName.Trim();
        guardian.RelationshipType = request.RelationshipType;
        guardian.NationalId = request.NationalId?.Trim();
        guardian.Email = request.Email?.Trim();
        guardian.Phone = request.Phone.Trim();
        guardian.AlternatePhone = request.AlternatePhone?.Trim();
        guardian.Occupation = request.Occupation?.Trim();
        guardian.AnnualIncome = request.AnnualIncome;
        guardian.Address = request.Address?.Trim();
        guardian.City = request.City?.Trim();
        guardian.IsEmergencyContact = request.IsEmergencyContact;

        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            AuditAction.Update, "Guardians", "Guardian", guardian.Id.ToString(),
            $"Updated guardian profile for '{guardian.FullName}'",
            organizationId: guardian.OrganizationId,
            branchId: guardian.BranchId,
            userId: CurrentUserService.UserId,
            userEmail: CurrentUserService.Email,
            cancellationToken: cancellationToken);

        return ApiSuccess("Guardian profile updated successfully.");
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(Permissions.ParentsDelete)]
    public async Task<IActionResult> DeleteGuardian(Guid id, CancellationToken cancellationToken)
    {
        var guardian = await _context.Guardians
            .Include(g => g.StudentGuardians)
            .FirstOrDefaultAsync(g => g.Id == id, cancellationToken);

        if (guardian == null)
            return ApiError("Guardian not found.", StatusCodes.Status404NotFound);

        if (!CurrentUserService.IsSuperAdmin && guardian.OrganizationId != CurrentUserService.OrganizationId)
            return ApiError("Unauthorized access to guardian outside your organization.", StatusCodes.Status403Forbidden);

        guardian.IsDeleted = true;
        foreach (var link in guardian.StudentGuardians)
        {
            link.IsDeleted = true;
        }

        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            AuditAction.Delete, "Guardians", "Guardian", guardian.Id.ToString(),
            $"Soft-deleted guardian profile for '{guardian.FullName}'",
            organizationId: guardian.OrganizationId,
            branchId: guardian.BranchId,
            userId: CurrentUserService.UserId,
            userEmail: CurrentUserService.Email,
            cancellationToken: cancellationToken);

        return ApiSuccess("Guardian profile deleted successfully.");
    }

    [HttpPost("{id:guid}/students")]
    [RequirePermission(Permissions.ParentsEdit)]
    public async Task<IActionResult> LinkStudent(Guid id, [FromBody] LinkStudentToGuardianRequestDto request, CancellationToken cancellationToken)
    {
        var guardian = await _context.Guardians.FirstOrDefaultAsync(g => g.Id == id, cancellationToken);
        if (guardian == null)
            return ApiError("Guardian not found.", StatusCodes.Status404NotFound);

        if (!CurrentUserService.IsSuperAdmin && guardian.OrganizationId != CurrentUserService.OrganizationId)
            return ApiError("Unauthorized access to guardian outside your organization.", StatusCodes.Status403Forbidden);

        var student = await _context.Students.FirstOrDefaultAsync(s => s.Id == request.StudentId && !s.IsDeleted, cancellationToken);
        if (student == null)
            return ApiError("Student not found.", StatusCodes.Status404NotFound);

        if (student.OrganizationId != guardian.OrganizationId)
            return ApiError("Student belongs to a different organization.", StatusCodes.Status400BadRequest);

        var existingLink = await _context.StudentGuardians
            .FirstOrDefaultAsync(sg => sg.GuardianId == id && sg.StudentId == request.StudentId, cancellationToken);

        if (request.IsPrimaryContact)
        {
            var otherPrimary = await _context.StudentGuardians
                .Where(sg => sg.StudentId == request.StudentId && sg.GuardianId != id && sg.IsPrimaryContact)
                .ToListAsync(cancellationToken);
            foreach (var op in otherPrimary)
            {
                op.IsPrimaryContact = false;
            }
        }

        if (existingLink != null)
        {
            existingLink.IsDeleted = false;
            existingLink.Relationship = request.Relationship;
            existingLink.IsPrimaryContact = request.IsPrimaryContact;
            existingLink.IsAuthorizedToPickup = request.IsAuthorizedToPickup;
        }
        else
        {
            var newLink = new StudentGuardian
            {
                Id = Guid.NewGuid(),
                OrganizationId = guardian.OrganizationId,
                BranchId = student.BranchId,
                GuardianId = id,
                StudentId = request.StudentId,
                Relationship = request.Relationship,
                IsPrimaryContact = request.IsPrimaryContact,
                IsAuthorizedToPickup = request.IsAuthorizedToPickup
            };
            _context.StudentGuardians.Add(newLink);
        }

        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            AuditAction.Create, "Guardians", "StudentGuardian", $"{id}:{request.StudentId}",
            $"Linked student '{student.FullName}' ({student.AdmissionNumber}) to guardian '{guardian.FullName}'",
            organizationId: guardian.OrganizationId,
            branchId: student.BranchId,
            userId: CurrentUserService.UserId,
            userEmail: CurrentUserService.Email,
            cancellationToken: cancellationToken);

        return ApiSuccess("Student linked to guardian successfully.");
    }

    [HttpDelete("{id:guid}/students/{studentId:guid}")]
    [RequirePermission(Permissions.ParentsEdit)]
    public async Task<IActionResult> UnlinkStudent(Guid id, Guid studentId, CancellationToken cancellationToken)
    {
        var guardian = await _context.Guardians.FirstOrDefaultAsync(g => g.Id == id, cancellationToken);
        if (guardian == null)
            return ApiError("Guardian not found.", StatusCodes.Status404NotFound);

        if (!CurrentUserService.IsSuperAdmin && guardian.OrganizationId != CurrentUserService.OrganizationId)
            return ApiError("Unauthorized access to guardian outside your organization.", StatusCodes.Status403Forbidden);

        var link = await _context.StudentGuardians
            .FirstOrDefaultAsync(sg => sg.GuardianId == id && sg.StudentId == studentId && !sg.IsDeleted, cancellationToken);

        if (link == null)
            return ApiError("Active relationship link between guardian and student not found.", StatusCodes.Status404NotFound);

        link.IsDeleted = true;
        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            AuditAction.Delete, "Guardians", "StudentGuardian", $"{id}:{studentId}",
            $"Unlinked student {studentId} from guardian '{guardian.FullName}'",
            organizationId: guardian.OrganizationId,
            branchId: guardian.BranchId,
            userId: CurrentUserService.UserId,
            userEmail: CurrentUserService.Email,
            cancellationToken: cancellationToken);

        return ApiSuccess("Student unlinked from guardian successfully.");
    }

    [HttpPost("{id:guid}/portal-access")]
    [RequirePermission(Permissions.ParentsEdit)]
    public async Task<IActionResult> ProvisionPortalAccess(
        Guid id,
        [FromBody] ProvisionParentUserRequestDto request,
        [FromServices] IPasswordHasher passwordHasher,
        CancellationToken cancellationToken)
    {
        var guardian = await _context.Guardians
            .Include(g => g.User)
            .FirstOrDefaultAsync(g => g.Id == id, cancellationToken);

        if (guardian == null)
            return ApiError("Guardian not found.", StatusCodes.Status404NotFound);

        if (!CurrentUserService.IsSuperAdmin && guardian.OrganizationId != CurrentUserService.OrganizationId)
            return ApiError("Unauthorized access to guardian outside your organization.", StatusCodes.Status403Forbidden);

        string email = (!string.IsNullOrWhiteSpace(request.Email) ? request.Email : guardian.Email)?.Trim().ToLowerInvariant()
            ?? $"{guardian.Phone.Replace("+", "").Replace(" ", "")}@parent.school";

        string password = !string.IsNullOrWhiteSpace(request.Password) ? request.Password : "Parent@123!";

        var parentRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == nameof(UserRoleType.Parent), cancellationToken);

        if (guardian.UserId.HasValue && guardian.User != null)
        {
            var (h, s) = passwordHasher.HashPassword(password);
            guardian.User.PasswordHash = h;
            guardian.User.PasswordSalt = s;
            guardian.User.IsActive = true;
            if (!string.IsNullOrWhiteSpace(request.Email))
            {
                guardian.User.Email = email;
                guardian.Email = email;
            }
            await _context.SaveChangesAsync(cancellationToken);
            return ApiSuccess(new { userId = guardian.User.Id, email = guardian.User.Email }, "Parent portal access credentials updated successfully.");
        }

        bool emailTaken = await _context.Users.AnyAsync(u => u.Email.ToLower() == email && !u.IsDeleted, cancellationToken);
        if (emailTaken)
            return ApiError($"User with email '{email}' already exists.");

        var (hash, salt) = passwordHasher.HashPassword(password);
        var user = new SchoolManagement.Domain.Entities.Security.User
        {
            Id = Guid.NewGuid(),
            OrganizationId = guardian.OrganizationId,
            BranchId = guardian.BranchId,
            FirstName = guardian.FirstName,
            LastName = guardian.LastName,
            Email = email,
            PhoneNumber = guardian.Phone,
            PasswordHash = hash,
            PasswordSalt = salt,
            RoleType = UserRoleType.Parent,
            IsActive = true
        };

        _context.Users.Add(user);
        guardian.UserId = user.Id;
        guardian.Email = email;

        if (parentRole != null)
        {
            _context.UserRoles.Add(new SchoolManagement.Domain.Entities.Security.UserRole
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                RoleId = parentRole.Id
            });
        }

        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            AuditAction.Create, "Guardians", "ParentUser", user.Id.ToString(),
            $"Provisioned parent portal access for '{guardian.FullName}' (User: {user.Email})",
            organizationId: guardian.OrganizationId,
            branchId: guardian.BranchId,
            userId: CurrentUserService.UserId,
            userEmail: CurrentUserService.Email,
            cancellationToken: cancellationToken);

        return ApiSuccess(new { userId = user.Id, email = user.Email }, "Parent portal user provisioned successfully.");
    }
}
