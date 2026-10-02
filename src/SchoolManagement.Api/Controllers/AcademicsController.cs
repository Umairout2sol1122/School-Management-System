using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Api.Authorization;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.Common.Models;
using SchoolManagement.Domain.Constants;
using SchoolManagement.Domain.Entities.Academics;
using SchoolManagement.Domain.Entities.Tenant;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AcademicsController : BaseApiController
{
    private readonly IApplicationDbContext _context;
    private readonly IAuditService _auditService;

    public AcademicsController(IApplicationDbContext context, IAuditService auditService)
    {
        _context = context;
        _auditService = auditService;
    }

    private Guid GetTargetOrgId(Guid? requestOrgId = null)
    {
        return CurrentUserService.IsSuperAdmin && requestOrgId.HasValue && requestOrgId.Value != Guid.Empty
            ? requestOrgId.Value
            : (CurrentUserService.OrganizationId ?? Guid.Empty);
    }

    // =========================================================================
    // 1. ACADEMIC SESSIONS
    // =========================================================================

    [HttpGet("sessions")]
    [RequirePermission(Permissions.AcademicsView)]
    public async Task<IActionResult> GetSessions([FromQuery] Guid? branchId, [FromQuery] bool? isActive, CancellationToken cancellationToken)
    {
        Guid targetOrgId = GetTargetOrgId();
        if (targetOrgId == Guid.Empty)
            return ApiError("Tenant context required.", StatusCodes.Status400BadRequest);

        var query = _context.AcademicSessions
            .Include(s => s.Branch)
            .Where(s => s.OrganizationId == targetOrgId && !s.IsDeleted);

        if (branchId.HasValue)
            query = query.Where(s => s.BranchId == branchId.Value);

        if (isActive.HasValue)
            query = query.Where(s => s.IsActive == isActive.Value);

        var sessions = await query
            .OrderByDescending(s => s.IsCurrent)
            .ThenByDescending(s => s.StartDate)
            .Select(s => new AcademicSessionDto(
                s.Id,
                s.OrganizationId,
                s.BranchId,
                s.Branch != null ? s.Branch.Name : null,
                s.Name,
                s.Code,
                s.Type,
                s.StartDate,
                s.EndDate,
                s.IsCurrent,
                s.IsActive
            ))
            .ToListAsync(cancellationToken);

        return ApiSuccess(sessions);
    }

    [HttpPost("sessions")]
    [RequirePermission(Permissions.AcademicsManage)]
    public async Task<IActionResult> CreateSession([FromBody] CreateAcademicSessionRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Code))
            return ApiError("Session name and code are required.");

        Guid targetOrgId = GetTargetOrgId();
        if (targetOrgId == Guid.Empty)
            return ApiError("Tenant context required.", StatusCodes.Status400BadRequest);

        string normCode = request.Code.Trim().ToUpperInvariant();
        bool codeExists = await _context.AcademicSessions.AnyAsync(s =>
            s.OrganizationId == targetOrgId &&
            s.Code.ToUpper() == normCode &&
            !s.IsDeleted, cancellationToken);

        if (codeExists)
            return ApiError($"An academic session with code '{request.Code}' already exists.");

        if (request.IsCurrent)
        {
            var currentSessions = await _context.AcademicSessions
                .Where(s => s.OrganizationId == targetOrgId && s.IsCurrent && !s.IsDeleted)
                .ToListAsync(cancellationToken);

            foreach (var s in currentSessions)
                s.IsCurrent = false;
        }

        var session = new AcademicSession
        {
            Id = Guid.NewGuid(),
            OrganizationId = targetOrgId,
            BranchId = request.BranchId ?? CurrentUserService.BranchId,
            Name = request.Name.Trim(),
            Code = normCode,
            Type = string.IsNullOrWhiteSpace(request.Type) ? "Annual" : request.Type.Trim(),
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            IsCurrent = request.IsCurrent,
            IsActive = true
        };

        _context.AcademicSessions.Add(session);
        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            AuditAction.Create, "Academics", "AcademicSession", session.Id.ToString(),
            $"Created academic session '{session.Name}' ({session.Code})",
            organizationId: targetOrgId,
            userId: CurrentUserService.UserId,
            userEmail: CurrentUserService.Email,
            cancellationToken: cancellationToken);

        return ApiSuccess(new { id = session.Id, code = session.Code }, "Academic session created successfully.");
    }

    [HttpPost("sessions/{id:guid}/set-current")]
    [RequirePermission(Permissions.AcademicsManage)]
    public async Task<IActionResult> SetCurrentSession(Guid id, CancellationToken cancellationToken)
    {
        Guid targetOrgId = GetTargetOrgId();
        var session = await _context.AcademicSessions
            .FirstOrDefaultAsync(s => s.Id == id && s.OrganizationId == targetOrgId && !s.IsDeleted, cancellationToken);

        if (session == null)
            return ApiError("Academic session not found.", StatusCodes.Status404NotFound);

        var existingCurrent = await _context.AcademicSessions
            .Where(s => s.OrganizationId == targetOrgId && s.IsCurrent && !s.IsDeleted)
            .ToListAsync(cancellationToken);

        foreach (var s in existingCurrent)
            s.IsCurrent = false;

        session.IsCurrent = true;
        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            AuditAction.Update, "Academics", "AcademicSession", session.Id.ToString(),
            $"Set academic session '{session.Name}' as the active current session",
            organizationId: targetOrgId,
            userId: CurrentUserService.UserId,
            userEmail: CurrentUserService.Email,
            cancellationToken: cancellationToken);

        return ApiSuccess("Session marked as current active academic session.");
    }

    [HttpDelete("sessions/{id:guid}")]
    [RequirePermission(Permissions.AcademicsManage)]
    public async Task<IActionResult> DeleteSession(Guid id, CancellationToken cancellationToken)
    {
        Guid targetOrgId = GetTargetOrgId();
        var session = await _context.AcademicSessions
            .FirstOrDefaultAsync(s => s.Id == id && s.OrganizationId == targetOrgId && !s.IsDeleted, cancellationToken);

        if (session == null)
            return ApiError("Academic session not found.", StatusCodes.Status404NotFound);

        session.IsDeleted = true;
        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            AuditAction.Delete, "Academics", "AcademicSession", session.Id.ToString(),
            $"Deleted academic session '{session.Name}'",
            organizationId: targetOrgId,
            userId: CurrentUserService.UserId,
            userEmail: CurrentUserService.Email,
            cancellationToken: cancellationToken);

        return ApiSuccess("Academic session deleted successfully.");
    }

    // =========================================================================
    // 2. DEPARTMENTS
    // =========================================================================

    [HttpGet("departments")]
    [RequirePermission(Permissions.AcademicsView)]
    public async Task<IActionResult> GetDepartments([FromQuery] bool? isActive, CancellationToken cancellationToken)
    {
        Guid targetOrgId = GetTargetOrgId();
        if (targetOrgId == Guid.Empty)
            return ApiError("Tenant context required.", StatusCodes.Status400BadRequest);

        var query = _context.Departments
            .Include(d => d.HeadOfDepartmentUser)
            .Where(d => d.OrganizationId == targetOrgId && !d.IsDeleted);

        if (isActive.HasValue)
            query = query.Where(d => d.IsActive == isActive.Value);

        var depts = await query
            .OrderBy(d => d.Name)
            .Select(d => new DepartmentDto(
                d.Id,
                d.OrganizationId,
                d.BranchId,
                d.Name,
                d.Code,
                d.Description,
                d.HeadOfDepartmentUserId,
                d.HeadOfDepartmentUser != null ? d.HeadOfDepartmentUser.FullName : null,
                d.IsActive,
                _context.StaffProfiles.Count(s => s.Department == d.Name && s.OrganizationId == targetOrgId && !s.IsDeleted),
                _context.SubjectCourses.Count(c => c.DepartmentId == d.Id && !c.IsDeleted)
            ))
            .ToListAsync(cancellationToken);

        return ApiSuccess(depts);
    }

    [HttpGet("departments/{id:guid}")]
    [RequirePermission(Permissions.AcademicsView)]
    public async Task<IActionResult> GetDepartmentById(Guid id, CancellationToken cancellationToken)
    {
        Guid targetOrgId = GetTargetOrgId();
        var d = await _context.Departments
            .Include(d => d.HeadOfDepartmentUser)
            .FirstOrDefaultAsync(d => d.Id == id && d.OrganizationId == targetOrgId && !d.IsDeleted, cancellationToken);

        if (d == null)
            return ApiError("Department not found.", StatusCodes.Status404NotFound);

        var dto = new DepartmentDto(
            d.Id,
            d.OrganizationId,
            d.BranchId,
            d.Name,
            d.Code,
            d.Description,
            d.HeadOfDepartmentUserId,
            d.HeadOfDepartmentUser != null ? d.HeadOfDepartmentUser.FullName : null,
            d.IsActive,
            await _context.StaffProfiles.CountAsync(s => s.Department == d.Name && s.OrganizationId == targetOrgId && !s.IsDeleted, cancellationToken),
            await _context.SubjectCourses.CountAsync(c => c.DepartmentId == d.Id && !c.IsDeleted, cancellationToken)
        );

        return ApiSuccess(dto);
    }

    [HttpPost("departments")]
    [RequirePermission(Permissions.AcademicsManage)]
    public async Task<IActionResult> CreateDepartment([FromBody] CreateDepartmentRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Code))
            return ApiError("Department name and code are required.");

        Guid targetOrgId = GetTargetOrgId();
        if (targetOrgId == Guid.Empty)
            return ApiError("Tenant context required.", StatusCodes.Status400BadRequest);

        string normCode = request.Code.Trim().ToUpperInvariant();
        bool exists = await _context.Departments.AnyAsync(d =>
            d.OrganizationId == targetOrgId &&
            d.Code.ToUpper() == normCode &&
            !d.IsDeleted, cancellationToken);

        if (exists)
            return ApiError($"A department with code '{request.Code}' already exists.");

        var dept = new Department
        {
            Id = Guid.NewGuid(),
            OrganizationId = targetOrgId,
            BranchId = request.BranchId ?? CurrentUserService.BranchId,
            Name = request.Name.Trim(),
            Code = normCode,
            Description = request.Description?.Trim(),
            HeadOfDepartmentUserId = request.HeadOfDepartmentUserId,
            IsActive = true
        };

        _context.Departments.Add(dept);
        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            AuditAction.Create, "Academics", "Department", dept.Id.ToString(),
            $"Created department '{dept.Name}' ({dept.Code})",
            organizationId: targetOrgId,
            userId: CurrentUserService.UserId,
            userEmail: CurrentUserService.Email,
            cancellationToken: cancellationToken);

        return ApiSuccess(new { id = dept.Id, code = dept.Code }, "Department created successfully.");
    }

    [HttpPut("departments/{id:guid}")]
    [RequirePermission(Permissions.AcademicsManage)]
    public async Task<IActionResult> UpdateDepartment(Guid id, [FromBody] UpdateDepartmentRequest request, CancellationToken cancellationToken)
    {
        Guid targetOrgId = GetTargetOrgId();
        var dept = await _context.Departments
            .FirstOrDefaultAsync(d => d.Id == id && d.OrganizationId == targetOrgId && !d.IsDeleted, cancellationToken);

        if (dept == null)
            return ApiError("Department not found.", StatusCodes.Status404NotFound);

        dept.Name = request.Name.Trim();
        dept.Description = request.Description?.Trim();
        dept.HeadOfDepartmentUserId = request.HeadOfDepartmentUserId;
        dept.IsActive = request.IsActive;

        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            AuditAction.Update, "Academics", "Department", dept.Id.ToString(),
            $"Updated department '{dept.Name}'",
            organizationId: targetOrgId,
            userId: CurrentUserService.UserId,
            userEmail: CurrentUserService.Email,
            cancellationToken: cancellationToken);

        return ApiSuccess("Department updated successfully.");
    }

    [HttpPatch("departments/{id:guid}/toggle-status")]
    [RequirePermission(Permissions.AcademicsManage)]
    public async Task<IActionResult> ToggleDepartmentStatus(Guid id, CancellationToken cancellationToken)
    {
        Guid targetOrgId = GetTargetOrgId();
        var dept = await _context.Departments
            .FirstOrDefaultAsync(d => d.Id == id && d.OrganizationId == targetOrgId && !d.IsDeleted, cancellationToken);

        if (dept == null)
            return ApiError("Department not found.", StatusCodes.Status404NotFound);

        dept.IsActive = !dept.IsActive;
        await _context.SaveChangesAsync(cancellationToken);

        return ApiSuccess(new { id = dept.Id, isActive = dept.IsActive }, $"Department status changed to {(dept.IsActive ? "Active" : "Inactive")}.");
    }

    [HttpDelete("departments/{id:guid}")]
    [RequirePermission(Permissions.AcademicsManage)]
    public async Task<IActionResult> DeleteDepartment(Guid id, CancellationToken cancellationToken)
    {
        Guid targetOrgId = GetTargetOrgId();
        var dept = await _context.Departments
            .FirstOrDefaultAsync(d => d.Id == id && d.OrganizationId == targetOrgId && !d.IsDeleted, cancellationToken);

        if (dept == null)
            return ApiError("Department not found.", StatusCodes.Status404NotFound);

        dept.IsDeleted = true;
        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            AuditAction.Delete, "Academics", "Department", dept.Id.ToString(),
            $"Deleted department '{dept.Name}'",
            organizationId: targetOrgId,
            userId: CurrentUserService.UserId,
            userEmail: CurrentUserService.Email,
            cancellationToken: cancellationToken);

        return ApiSuccess("Department deleted successfully.");
    }

    // =========================================================================
    // 3. CLASS / GRADE MANAGEMENT
    // =========================================================================

    [HttpGet("classes")]
    [HttpGet("grades")]
    [HttpGet("programs")]
    [RequirePermission(Permissions.AcademicsView)]
    public async Task<IActionResult> GetClasses([FromQuery] Guid? departmentId, [FromQuery] bool? isActive, CancellationToken cancellationToken)
    {
        Guid targetOrgId = GetTargetOrgId();
        if (targetOrgId == Guid.Empty)
            return ApiError("Tenant context required.", StatusCodes.Status400BadRequest);

        var query = _context.ClassGrades
            .Include(c => c.Department)
            .Include(c => c.Sections)
            .Where(c => c.OrganizationId == targetOrgId && !c.IsDeleted);

        if (departmentId.HasValue)
            query = query.Where(c => c.DepartmentId == departmentId.Value);

        if (isActive.HasValue)
            query = query.Where(c => c.IsActive == isActive.Value);

        var list = await query
            .OrderBy(c => c.DisplayOrder)
            .ThenBy(c => c.Name)
            .Select(c => new ClassGradeDto(
                c.Id,
                c.OrganizationId,
                c.BranchId,
                c.DepartmentId,
                c.Department != null ? c.Department.Name : null,
                c.Name,
                c.Code,
                c.DisplayOrder,
                c.Capacity,
                c.IsActive,
                c.Sections.Count(s => !s.IsDeleted),
                _context.Students.Count(s => s.CurrentGrade == c.Name && s.OrganizationId == targetOrgId && !s.IsDeleted)
            ))
            .ToListAsync(cancellationToken);

        return ApiSuccess(list);
    }

    [HttpPost("classes")]
    [HttpPost("grades")]
    [HttpPost("programs")]
    [RequirePermission(Permissions.AcademicsManage)]
    public async Task<IActionResult> CreateClass([FromBody] CreateClassGradeRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Code))
            return ApiError("Class name and code are required.");

        Guid targetOrgId = GetTargetOrgId();
        if (targetOrgId == Guid.Empty)
            return ApiError("Tenant context required.", StatusCodes.Status400BadRequest);

        string normCode = !string.IsNullOrWhiteSpace(request.Code) ? request.Code.Trim().ToUpperInvariant() : request.Name.Trim().ToUpperInvariant().Replace(" ", "-");
        var existingClass = await _context.ClassGrades.FirstOrDefaultAsync(c =>
            c.OrganizationId == targetOrgId &&
            c.Code.ToUpper() == normCode &&
            !c.IsDeleted, cancellationToken);

        if (existingClass != null)
            return ApiSuccess(new { id = existingClass.Id, code = existingClass.Code }, "Class/Grade already exists.");

        var classGrade = new ClassGrade
        {
            Id = Guid.NewGuid(),
            OrganizationId = targetOrgId,
            BranchId = request.BranchId ?? CurrentUserService.BranchId,
            DepartmentId = request.DepartmentId,
            Name = request.Name.Trim(),
            Code = normCode,
            DisplayOrder = request.DisplayOrder,
            Capacity = request.Capacity > 0 ? request.Capacity : 40,
            IsActive = true
        };

        // Add a default Section A
        classGrade.Sections.Add(new ClassSection
        {
            Id = Guid.NewGuid(),
            OrganizationId = targetOrgId,
            ClassGradeId = classGrade.Id,
            BranchId = classGrade.BranchId,
            Name = "Section A",
            Capacity = classGrade.Capacity,
            IsActive = true
        });

        _context.ClassGrades.Add(classGrade);
        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            AuditAction.Create, "Academics", "ClassGrade", classGrade.Id.ToString(),
            $"Created class/grade '{classGrade.Name}' ({classGrade.Code}) with default section",
            organizationId: targetOrgId,
            userId: CurrentUserService.UserId,
            userEmail: CurrentUserService.Email,
            cancellationToken: cancellationToken);

        return ApiSuccess(new { id = classGrade.Id, code = classGrade.Code }, "Class/Grade created successfully.");
    }

    [HttpPut("classes/{id:guid}")]
    [RequirePermission(Permissions.AcademicsManage)]
    public async Task<IActionResult> UpdateClass(Guid id, [FromBody] UpdateClassGradeRequest request, CancellationToken cancellationToken)
    {
        Guid targetOrgId = GetTargetOrgId();
        var c = await _context.ClassGrades
            .FirstOrDefaultAsync(cg => cg.Id == id && cg.OrganizationId == targetOrgId && !cg.IsDeleted, cancellationToken);

        if (c == null)
            return ApiError("Class/Grade not found.", StatusCodes.Status404NotFound);

        c.Name = request.Name.Trim();
        c.DepartmentId = request.DepartmentId;
        c.DisplayOrder = request.DisplayOrder;
        c.Capacity = request.Capacity > 0 ? request.Capacity : c.Capacity;
        c.IsActive = request.IsActive;

        await _context.SaveChangesAsync(cancellationToken);

        return ApiSuccess("Class/Grade updated successfully.");
    }

    [HttpPatch("classes/{id:guid}/toggle-status")]
    [RequirePermission(Permissions.AcademicsManage)]
    public async Task<IActionResult> ToggleClassStatus(Guid id, CancellationToken cancellationToken)
    {
        Guid targetOrgId = GetTargetOrgId();
        var c = await _context.ClassGrades
            .FirstOrDefaultAsync(cg => cg.Id == id && cg.OrganizationId == targetOrgId && !cg.IsDeleted, cancellationToken);

        if (c == null)
            return ApiError("Class/Grade not found.", StatusCodes.Status404NotFound);

        c.IsActive = !c.IsActive;
        await _context.SaveChangesAsync(cancellationToken);

        return ApiSuccess(new { id = c.Id, isActive = c.IsActive }, $"Class status changed to {(c.IsActive ? "Active" : "Inactive")}.");
    }

    [HttpDelete("classes/{id:guid}")]
    [RequirePermission(Permissions.AcademicsManage)]
    public async Task<IActionResult> DeleteClass(Guid id, CancellationToken cancellationToken)
    {
        Guid targetOrgId = GetTargetOrgId();
        var c = await _context.ClassGrades
            .FirstOrDefaultAsync(cg => cg.Id == id && cg.OrganizationId == targetOrgId && !cg.IsDeleted, cancellationToken);

        if (c == null)
            return ApiError("Class/Grade not found.", StatusCodes.Status404NotFound);

        c.IsDeleted = true;
        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            AuditAction.Delete, "Academics", "ClassGrade", c.Id.ToString(),
            $"Deleted class/grade '{c.Name}'",
            organizationId: targetOrgId,
            userId: CurrentUserService.UserId,
            userEmail: CurrentUserService.Email,
            cancellationToken: cancellationToken);

        return ApiSuccess("Class/Grade deleted successfully.");
    }

    // =========================================================================
    // 4. CLASS SECTIONS
    // =========================================================================

    [HttpGet("classes/{classId:guid}/sections")]
    [RequirePermission(Permissions.AcademicsView)]
    public async Task<IActionResult> GetSections(Guid classId, CancellationToken cancellationToken)
    {
        Guid targetOrgId = GetTargetOrgId();
        var sections = await _context.ClassSections
            .Include(s => s.ClassGrade)
            .Include(s => s.ClassTeacherUser)
            .Where(s => s.ClassGradeId == classId && s.OrganizationId == targetOrgId && !s.IsDeleted)
            .OrderBy(s => s.Name)
            .Select(s => new ClassSectionDto(
                s.Id,
                s.ClassGradeId,
                s.ClassGrade != null ? s.ClassGrade.Name : string.Empty,
                s.BranchId,
                s.Name,
                s.RoomNumber,
                s.Capacity,
                s.ClassTeacherUserId,
                s.ClassTeacherUser != null ? s.ClassTeacherUser.FullName : null,
                s.IsActive,
                _context.Students.Count(stu => stu.Section == s.Name && stu.CurrentGrade == (s.ClassGrade != null ? s.ClassGrade.Name : "") && !stu.IsDeleted)
            ))
            .ToListAsync(cancellationToken);

        return ApiSuccess(sections);
    }

    [HttpPost("sections")]
    [HttpPost("classes/{classId:guid}/sections")]
    [HttpPost("batches")]
    [RequirePermission(Permissions.AcademicsManage)]
    public async Task<IActionResult> CreateSection([FromRoute] Guid? classId, [FromBody] CreateClassSectionRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return ApiError("Section name is required.");

        Guid targetClassId = classId ?? request.EffectiveClassGradeId ?? Guid.Empty;
        if (targetClassId == Guid.Empty)
            return ApiError("ClassGradeId is required.");

        Guid targetOrgId = GetTargetOrgId();
        var classGrade = await _context.ClassGrades
            .FirstOrDefaultAsync(c => c.Id == targetClassId && c.OrganizationId == targetOrgId && !c.IsDeleted, cancellationToken);

        if (classGrade == null)
            return ApiError("Class/Grade not found.", StatusCodes.Status404NotFound);

        var existingSection = await _context.ClassSections.FirstOrDefaultAsync(s =>
            s.ClassGradeId == targetClassId &&
            s.Name.ToLower() == request.Name.Trim().ToLower() &&
            !s.IsDeleted, cancellationToken);

        if (existingSection != null)
            return ApiSuccess(new { id = existingSection.Id, name = existingSection.Name }, $"Section '{request.Name}' already exists for this class.");

        var section = new ClassSection
        {
            Id = Guid.NewGuid(),
            OrganizationId = targetOrgId,
            ClassGradeId = targetClassId,
            BranchId = request.BranchId ?? classGrade.BranchId,
            Name = request.Name.Trim(),
            RoomNumber = request.RoomNumber?.Trim(),
            Capacity = request.Capacity > 0 ? request.Capacity : classGrade.Capacity,
            ClassTeacherUserId = request.ClassTeacherUserId,
            IsActive = true
        };

        _context.ClassSections.Add(section);
        await _context.SaveChangesAsync(cancellationToken);

        return ApiSuccess(new { id = section.Id, name = section.Name }, "Section added successfully.");
    }

    [HttpDelete("sections/{id:guid}")]
    [RequirePermission(Permissions.AcademicsManage)]
    public async Task<IActionResult> DeleteSection(Guid id, CancellationToken cancellationToken)
    {
        Guid targetOrgId = GetTargetOrgId();
        var section = await _context.ClassSections
            .FirstOrDefaultAsync(s => s.Id == id && s.OrganizationId == targetOrgId && !s.IsDeleted, cancellationToken);

        if (section == null)
            return ApiError("Section not found.", StatusCodes.Status404NotFound);

        section.IsDeleted = true;
        await _context.SaveChangesAsync(cancellationToken);

        return ApiSuccess("Section deleted successfully.");
    }

    // =========================================================================
    // 5. SUBJECTS / COURSES
    // =========================================================================

    [HttpGet("subjects")]
    [HttpGet("courses")]
    [HttpGet("modules")]
    [RequirePermission(Permissions.AcademicsView)]
    public async Task<IActionResult> GetSubjects(
        [FromQuery] Guid? departmentId,
        [FromQuery] Guid? classGradeId,
        [FromQuery] bool? isActive,
        CancellationToken cancellationToken)
    {
        Guid targetOrgId = GetTargetOrgId();
        if (targetOrgId == Guid.Empty)
            return ApiError("Tenant context required.", StatusCodes.Status400BadRequest);

        var query = _context.SubjectCourses
            .Include(s => s.Department)
            .Include(s => s.ClassGrade)
            .Where(s => s.OrganizationId == targetOrgId && !s.IsDeleted);

        if (departmentId.HasValue)
            query = query.Where(s => s.DepartmentId == departmentId.Value);

        if (classGradeId.HasValue)
            query = query.Where(s => s.ClassGradeId == classGradeId.Value);

        if (isActive.HasValue)
            query = query.Where(s => s.IsActive == isActive.Value);

        var subjects = await query
            .OrderBy(s => s.Code)
            .Select(s => new SubjectCourseDto(
                s.Id,
                s.OrganizationId,
                s.BranchId,
                s.DepartmentId,
                s.Department != null ? s.Department.Name : null,
                s.ClassGradeId,
                s.ClassGrade != null ? s.ClassGrade.Name : null,
                s.Name,
                s.Code,
                s.CreditHours,
                s.Type,
                s.IsActive
            ))
            .ToListAsync(cancellationToken);

        return ApiSuccess(subjects);
    }

    [HttpPost("subjects")]
    [HttpPost("courses")]
    [HttpPost("modules")]
    [RequirePermission(Permissions.AcademicsManage)]
    public async Task<IActionResult> CreateSubject([FromBody] CreateSubjectCourseRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Code))
            return ApiError("Subject/course name and code are required.");

        Guid targetOrgId = GetTargetOrgId();
        if (targetOrgId == Guid.Empty)
            return ApiError("Tenant context required.", StatusCodes.Status400BadRequest);

        string normCode = !string.IsNullOrWhiteSpace(request.Code) ? request.Code.Trim().ToUpperInvariant() : request.Name.Trim().ToUpperInvariant().Replace(" ", "-");
        var existingSubj = await _context.SubjectCourses.FirstOrDefaultAsync(s =>
            s.OrganizationId == targetOrgId &&
            s.Code.ToUpper() == normCode &&
            !s.IsDeleted, cancellationToken);

        if (existingSubj != null)
            return ApiSuccess(new { id = existingSubj.Id, code = existingSubj.Code }, $"Subject '{request.Name}' already exists.");

        var subject = new SubjectCourse
        {
            Id = Guid.NewGuid(),
            OrganizationId = targetOrgId,
            BranchId = request.BranchId ?? CurrentUserService.BranchId,
            DepartmentId = request.DepartmentId,
            ClassGradeId = request.EffectiveClassGradeId,
            Name = request.Name.Trim(),
            Code = normCode,
            CreditHours = request.CreditHours > 0 ? request.CreditHours : 3,
            Type = string.IsNullOrWhiteSpace(request.Type) ? "Core" : request.Type.Trim(),
            IsActive = true
        };

        _context.SubjectCourses.Add(subject);
        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            AuditAction.Create, "Academics", "SubjectCourse", subject.Id.ToString(),
            $"Created subject/course '{subject.Name}' ({subject.Code})",
            organizationId: targetOrgId,
            userId: CurrentUserService.UserId,
            userEmail: CurrentUserService.Email,
            cancellationToken: cancellationToken);

        return ApiSuccess(new { id = subject.Id, code = subject.Code }, "Subject/Course created successfully.");
    }

    [HttpDelete("subjects/{id:guid}")]
    [RequirePermission(Permissions.AcademicsManage)]
    public async Task<IActionResult> DeleteSubject(Guid id, CancellationToken cancellationToken)
    {
        Guid targetOrgId = GetTargetOrgId();
        var s = await _context.SubjectCourses
            .FirstOrDefaultAsync(sub => sub.Id == id && sub.OrganizationId == targetOrgId && !sub.IsDeleted, cancellationToken);

        if (s == null)
            return ApiError("Subject/Course not found.", StatusCodes.Status404NotFound);

        s.IsDeleted = true;
        await _context.SaveChangesAsync(cancellationToken);

        return ApiSuccess("Subject/Course deleted successfully.");
    }

    // =========================================================================
    // 6. COURSE ASSIGNMENTS (Teacher -> Section -> Subject)
    // =========================================================================

    [HttpGet("assignments")]
    [RequirePermission(Permissions.AcademicsView)]
    public async Task<IActionResult> GetAssignments([FromQuery] Guid? sectionId, [FromQuery] Guid? teacherId, CancellationToken cancellationToken)
    {
        Guid targetOrgId = GetTargetOrgId();
        var query = _context.CourseAssignments
            .Include(a => a.ClassSection)
                .ThenInclude(sec => sec!.ClassGrade)
            .Include(a => a.SubjectCourse)
            .Include(a => a.TeacherUser)
            .Include(a => a.AcademicSession)
            .Where(a => a.OrganizationId == targetOrgId && !a.IsDeleted);

        if (sectionId.HasValue)
            query = query.Where(a => a.ClassSectionId == sectionId.Value);

        if (teacherId.HasValue)
            query = query.Where(a => a.TeacherUserId == teacherId.Value);

        var list = await query
            .OrderBy(a => a.ClassSection!.Name)
            .ThenBy(a => a.SubjectCourse!.Name)
            .Select(a => new CourseAssignmentDto(
                a.Id,
                a.OrganizationId,
                a.ClassSectionId,
                a.ClassSection != null ? a.ClassSection.Name : string.Empty,
                a.ClassSection != null && a.ClassSection.ClassGrade != null ? a.ClassSection.ClassGrade.Name : string.Empty,
                a.SubjectCourseId,
                a.SubjectCourse != null ? a.SubjectCourse.Name : string.Empty,
                a.SubjectCourse != null ? a.SubjectCourse.Code : string.Empty,
                a.TeacherUserId,
                a.TeacherUser != null ? a.TeacherUser.FullName : string.Empty,
                a.AcademicSessionId,
                a.AcademicSession != null ? a.AcademicSession.Name : null,
                a.IsActive
            ))
            .ToListAsync(cancellationToken);

        return ApiSuccess(list);
    }

    [HttpPost("assignments")]
    [RequirePermission(Permissions.AcademicsManage)]
    public async Task<IActionResult> CreateAssignment([FromBody] CreateCourseAssignmentRequest request, CancellationToken cancellationToken)
    {
        Guid targetOrgId = GetTargetOrgId();

        var section = await _context.ClassSections
            .Include(s => s.ClassGrade)
            .FirstOrDefaultAsync(s => s.Id == request.ClassSectionId && s.OrganizationId == targetOrgId && !s.IsDeleted, cancellationToken);
        if (section == null)
            return ApiError("Class section not found.");

        if (!section.IsActive)
            return ApiError("Cannot assign teacher to an inactive class section.");

        if (section.ClassGrade != null && !section.ClassGrade.IsActive)
            return ApiError("Cannot assign teacher to a section belonging to an inactive class/grade.");

        var subject = await _context.SubjectCourses.FirstOrDefaultAsync(sub => sub.Id == request.SubjectCourseId && sub.OrganizationId == targetOrgId && !sub.IsDeleted, cancellationToken);
        if (subject == null)
            return ApiError("Subject/Course not found.");

        if (!subject.IsActive)
            return ApiError("Cannot assign teacher to an inactive subject/course.");

        var teacher = await _context.Users.FirstOrDefaultAsync(u => u.Id == request.TeacherUserId && u.OrganizationId == targetOrgId && !u.IsDeleted, cancellationToken);
        if (teacher == null)
            return ApiError("Teacher user not found.");

        if (!teacher.IsActive)
            return ApiError("Cannot assign an inactive teacher user to a course.");

        var existingAssignment = await _context.CourseAssignments.FirstOrDefaultAsync(ca =>
            ca.OrganizationId == targetOrgId &&
            ca.ClassSectionId == request.ClassSectionId &&
            ca.SubjectCourseId == request.SubjectCourseId &&
            ca.TeacherUserId == request.TeacherUserId &&
            !ca.IsDeleted, cancellationToken);

        if (existingAssignment != null)
        {
            existingAssignment.IsActive = true;
            await _context.SaveChangesAsync(cancellationToken);
            return ApiSuccess(new { id = existingAssignment.Id }, "Teacher assigned to course successfully.");
        }

        var assignment = new CourseAssignment
        {
            Id = Guid.NewGuid(),
            OrganizationId = targetOrgId,
            BranchId = section.BranchId,
            ClassSectionId = request.ClassSectionId,
            SubjectCourseId = request.SubjectCourseId,
            TeacherUserId = request.TeacherUserId,
            AcademicSessionId = request.AcademicSessionId,
            IsActive = true
        };

        _context.CourseAssignments.Add(assignment);
        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            AuditAction.Create, "Academics", "CourseAssignment", assignment.Id.ToString(),
            $"Assigned teacher '{teacher.FullName}' to teach '{subject.Name}' in section '{section.Name}'",
            organizationId: targetOrgId,
            userId: CurrentUserService.UserId,
            userEmail: CurrentUserService.Email,
            cancellationToken: cancellationToken);

        return ApiSuccess(new { id = assignment.Id }, "Course assignment created successfully.");
    }

    [HttpDelete("assignments/{id:guid}")]
    [RequirePermission(Permissions.AcademicsManage)]
    public async Task<IActionResult> DeleteAssignment(Guid id, CancellationToken cancellationToken)
    {
        Guid targetOrgId = GetTargetOrgId();
        var assignment = await _context.CourseAssignments
            .FirstOrDefaultAsync(a => a.Id == id && a.OrganizationId == targetOrgId && !a.IsDeleted, cancellationToken);

        if (assignment == null)
            return ApiError("Course assignment not found.", StatusCodes.Status404NotFound);

        assignment.IsDeleted = true;
        await _context.SaveChangesAsync(cancellationToken);

        return ApiSuccess("Course assignment removed successfully.");
    }

    // =========================================================================
    // 7. TIMETABLE MANAGEMENT
    // =========================================================================

    [HttpGet("timetable")]
    [RequirePermission(Permissions.TimetableView)]
    public async Task<IActionResult> GetTimetableSlots([FromQuery] Guid? classGradeId, [FromQuery] Guid? branchId, [FromQuery] DayOfWeek? dayOfWeek, CancellationToken cancellationToken)
    {
        Guid targetOrgId = GetTargetOrgId();
        if (targetOrgId == Guid.Empty) return ApiError("Tenant context required.", StatusCodes.Status400BadRequest);

        var query = _context.TimetableSlots
            .Include(t => t.ClassGrade)
            .Include(t => t.ClassSection)
            .Include(t => t.SubjectCourse)
            .Include(t => t.TeacherStaff).ThenInclude(ts => ts!.User)
            .Where(t => t.OrganizationId == targetOrgId && !t.IsDeleted);

        if (classGradeId.HasValue) query = query.Where(t => t.ClassGradeId == classGradeId.Value);
        if (branchId.HasValue) query = query.Where(t => t.BranchId == branchId.Value);
        if (dayOfWeek.HasValue) query = query.Where(t => t.DayOfWeek == dayOfWeek.Value);

        var slots = await query
            .OrderBy(t => t.DayOfWeek)
            .ThenBy(t => t.StartTime)
            .Select(t => new TeacherTimetableItemDto(
                t.Id,
                t.DayOfWeek,
                t.DayOfWeek.ToString(),
                t.StartTime,
                t.EndTime,
                t.ClassGrade != null ? t.ClassGrade.Name : "Class",
                t.ClassSection != null ? t.ClassSection.Name : null,
                t.SubjectCourse != null ? t.SubjectCourse.Name : "Subject",
                t.RoomNumber
            ))
            .ToListAsync(cancellationToken);

        return ApiSuccess(slots);
    }

    [HttpPost("timetable")]
    [RequirePermission(Permissions.TimetableManage)]
    public async Task<IActionResult> CreateTimetableSlot([FromBody] CreateTimetableSlotRequest request, CancellationToken cancellationToken)
    {
        Guid targetOrgId = GetTargetOrgId();
        if (targetOrgId == Guid.Empty) return ApiError("Tenant context required.", StatusCodes.Status400BadRequest);

        if (request.EndTime <= request.StartTime)
            return ApiError("End time must be after start time.", StatusCodes.Status400BadRequest);

        var classGrade = await _context.ClassGrades.FirstOrDefaultAsync(c => c.Id == request.ClassGradeId && c.OrganizationId == targetOrgId && !c.IsDeleted, cancellationToken);
        if (classGrade == null) return ApiError("Class grade not found.");

        if (!classGrade.IsActive)
            return ApiError("Cannot schedule timetable slot for an inactive class/grade.");

        var subject = await _context.SubjectCourses.FirstOrDefaultAsync(s => s.Id == request.SubjectCourseId && s.OrganizationId == targetOrgId && !s.IsDeleted, cancellationToken);
        if (subject == null) return ApiError("Subject course not found.");

        if (!subject.IsActive)
            return ApiError("Cannot schedule timetable slot for an inactive subject/course.");

        Guid? resolvedStaffId = request.TeacherStaffId;
        if (request.TeacherStaffId.HasValue)
        {
            var staffProfile = await _context.StaffProfiles
                .FirstOrDefaultAsync(sp => sp.OrganizationId == targetOrgId &&
                    (sp.Id == request.TeacherStaffId.Value || sp.UserId == request.TeacherStaffId.Value) &&
                    !sp.IsDeleted, cancellationToken);
            if (staffProfile != null)
                resolvedStaffId = staffProfile.Id;
        }

        // Conflict Detection: Teacher schedule collision
        if (resolvedStaffId.HasValue)
        {
            bool teacherConflict = await _context.TimetableSlots.AnyAsync(t =>
                t.OrganizationId == targetOrgId &&
                t.TeacherStaffId == resolvedStaffId.Value &&
                t.DayOfWeek == request.DayOfWeek &&
                t.StartTime < request.EndTime &&
                t.EndTime > request.StartTime &&
                t.IsActive &&
                !t.IsDeleted, cancellationToken);

            if (teacherConflict)
                return ApiError("Teacher conflict detected: The selected instructor is already scheduled for another class during this time period.", StatusCodes.Status400BadRequest);
        }

        // Conflict Detection: Class/Section collision
        bool classConflict = await _context.TimetableSlots.AnyAsync(t =>
            t.OrganizationId == targetOrgId &&
            t.ClassGradeId == request.ClassGradeId &&
            t.ClassSectionId == request.ClassSectionId &&
            t.DayOfWeek == request.DayOfWeek &&
            t.StartTime < request.EndTime &&
            t.EndTime > request.StartTime &&
            t.IsActive &&
            !t.IsDeleted, cancellationToken);

        if (classConflict)
            return ApiError("Class conflict detected: This class section already has another scheduled subject during this time period.", StatusCodes.Status400BadRequest);

        // Conflict Detection: Room collision
        if (!string.IsNullOrWhiteSpace(request.RoomNumber))
        {
            string normRoom = request.RoomNumber.Trim().ToLower();
            bool roomConflict = await _context.TimetableSlots.AnyAsync(t =>
                t.OrganizationId == targetOrgId &&
                t.RoomNumber != null &&
                t.RoomNumber.ToLower() == normRoom &&
                t.DayOfWeek == request.DayOfWeek &&
                t.StartTime < request.EndTime &&
                t.EndTime > request.StartTime &&
                t.IsActive &&
                !t.IsDeleted, cancellationToken);

            if (roomConflict)
                return ApiError($"Room conflict detected: Room '{request.RoomNumber}' is already booked during this time period.", StatusCodes.Status400BadRequest);
        }

        var slot = new TimetableSlot
        {
            Id = Guid.NewGuid(),
            OrganizationId = targetOrgId,
            BranchId = classGrade.BranchId,
            AcademicSessionId = request.AcademicSessionId,
            ClassGradeId = request.ClassGradeId,
            ClassSectionId = request.ClassSectionId,
            SubjectCourseId = request.SubjectCourseId,
            TeacherStaffId = resolvedStaffId,
            DayOfWeek = request.DayOfWeek,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            RoomNumber = request.RoomNumber,
            IsActive = true
        };

        _context.TimetableSlots.Add(slot);
        await _context.SaveChangesAsync(cancellationToken);

        return ApiSuccess(new { id = slot.Id }, "Timetable slot created successfully.");
    }

    [HttpDelete("timetable/{id:guid}")]
    [RequirePermission(Permissions.TimetableManage)]
    public async Task<IActionResult> DeleteTimetableSlot(Guid id, CancellationToken cancellationToken)
    {
        Guid targetOrgId = GetTargetOrgId();
        var slot = await _context.TimetableSlots.FirstOrDefaultAsync(t => t.Id == id && t.OrganizationId == targetOrgId && !t.IsDeleted, cancellationToken);
        if (slot == null) return ApiNotFound("Timetable slot not found.");

        slot.IsDeleted = true;
        await _context.SaveChangesAsync(cancellationToken);

        return ApiSuccess("Timetable slot deleted successfully.");
    }

    [HttpPut("timetable/{id:guid}")]
    [RequirePermission(Permissions.TimetableManage)]
    public async Task<IActionResult> UpdateTimetableSlot(Guid id, [FromBody] UpdateTimetableSlotRequest request, CancellationToken cancellationToken)
    {
        Guid targetOrgId = GetTargetOrgId();
        if (targetOrgId == Guid.Empty) return ApiError("Tenant context required.", StatusCodes.Status400BadRequest);

        var slot = await _context.TimetableSlots.FirstOrDefaultAsync(t => t.Id == id && t.OrganizationId == targetOrgId && !t.IsDeleted, cancellationToken);
        if (slot == null) return ApiNotFound("Timetable slot not found.");

        if (request.EndTime <= request.StartTime)
            return ApiError("End time must be after start time.", StatusCodes.Status400BadRequest);

        Guid? resolvedStaffId = request.TeacherStaffId ?? slot.TeacherStaffId;
        if (resolvedStaffId.HasValue)
        {
            bool teacherConflict = await _context.TimetableSlots.AnyAsync(t =>
                t.OrganizationId == targetOrgId &&
                t.Id != id &&
                t.TeacherStaffId == resolvedStaffId.Value &&
                t.DayOfWeek == slot.DayOfWeek &&
                t.StartTime < request.EndTime &&
                t.EndTime > request.StartTime &&
                t.IsActive && !t.IsDeleted, cancellationToken);

            if (teacherConflict)
                return ApiError("Teacher conflict detected: The selected instructor is already scheduled for another class during this time period.", StatusCodes.Status400BadRequest);
        }

        string? normRoom = request.RoomNumber?.Trim();
        if (!string.IsNullOrWhiteSpace(normRoom))
        {
            bool roomConflict = await _context.TimetableSlots.AnyAsync(t =>
                t.OrganizationId == targetOrgId &&
                t.Id != id &&
                t.RoomNumber != null && t.RoomNumber.ToLower() == normRoom.ToLower() &&
                t.DayOfWeek == slot.DayOfWeek &&
                t.StartTime < request.EndTime &&
                t.EndTime > request.StartTime &&
                t.IsActive && !t.IsDeleted, cancellationToken);

            if (roomConflict)
                return ApiError($"Room conflict detected: Room '{normRoom}' is already booked during this time period.", StatusCodes.Status400BadRequest);
        }

        slot.StartTime = request.StartTime;
        slot.EndTime = request.EndTime;
        slot.RoomNumber = normRoom ?? slot.RoomNumber;
        slot.TeacherStaffId = resolvedStaffId;

        await _context.SaveChangesAsync(cancellationToken);
        return ApiSuccess("Timetable slot updated successfully.");
    }

    [HttpPost("timetable/{id:guid}/substitute")]
    [RequirePermission(Permissions.TimetableManage)]
    public async Task<IActionResult> AssignSubstituteTeacher(Guid id, [FromBody] AssignSubstituteTeacherRequest request, CancellationToken cancellationToken)
    {
        Guid targetOrgId = GetTargetOrgId();
        if (targetOrgId == Guid.Empty) return ApiError("Tenant context required.", StatusCodes.Status400BadRequest);

        var slot = await _context.TimetableSlots.FirstOrDefaultAsync(t => t.Id == id && t.OrganizationId == targetOrgId && !t.IsDeleted, cancellationToken);
        if (slot == null) return ApiNotFound("Timetable slot not found.");

        var subStaff = await _context.StaffProfiles.Include(s => s.User)
            .FirstOrDefaultAsync(sp => sp.OrganizationId == targetOrgId && (sp.Id == request.SubstituteTeacherStaffId || sp.UserId == request.SubstituteTeacherStaffId) && !sp.IsDeleted, cancellationToken);

        if (subStaff == null)
            return ApiNotFound("Substitute instructor not found in staff roster.");

        bool conflict = await _context.TimetableSlots.AnyAsync(t =>
            t.OrganizationId == targetOrgId &&
            t.Id != id &&
            (t.TeacherStaffId == subStaff.Id || t.SubstituteTeacherStaffId == subStaff.Id) &&
            t.DayOfWeek == slot.DayOfWeek &&
            t.StartTime < slot.EndTime &&
            t.EndTime > slot.StartTime &&
            t.IsActive && !t.IsDeleted, cancellationToken);

        if (conflict)
            return ApiError($"Substitute conflict: Instructor '{subStaff.User?.FullName ?? "Staff"}' is already teaching during this period.", StatusCodes.Status400BadRequest);

        slot.SubstituteTeacherStaffId = subStaff.Id;
        slot.SubstitutionNotes = request.SubstitutionNotes?.Trim();

        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            AuditAction.Update, "Academics", "TimetableSubstitution", slot.Id.ToString(),
            $"Assigned substitute teacher '{subStaff.User?.FullName}' for timetable slot on {slot.DayOfWeek} ({slot.StartTime}-{slot.EndTime}). Notes: {slot.SubstitutionNotes}",
            organizationId: targetOrgId,
            branchId: slot.BranchId,
            userId: CurrentUserService.UserId,
            userEmail: CurrentUserService.Email,
            cancellationToken: cancellationToken);

        return ApiSuccess($"Substitute teacher '{subStaff.User?.FullName}' successfully assigned.");
    }

    // =========================================================================
    // 7.1 STUDENT PROMOTION & RETENTION
    // =========================================================================

    [HttpPost("promotion/promote")]
    [RequirePermission(Permissions.AcademicsManage)]
    public async Task<IActionResult> PromoteStudents([FromBody] PromoteStudentsRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.FromGrade) || string.IsNullOrWhiteSpace(request.ToGrade))
            return ApiError("Source grade and target promotion grade are required.");

        Guid targetOrgId = GetTargetOrgId();
        if (targetOrgId == Guid.Empty) return ApiError("Tenant context required.", StatusCodes.Status400BadRequest);

        var targetGrade = await _context.ClassGrades
            .FirstOrDefaultAsync(cg => cg.OrganizationId == targetOrgId && cg.Name.ToLower() == request.ToGrade.Trim().ToLower() && !cg.IsDeleted, cancellationToken);

        if (targetGrade == null)
            return ApiError($"Target grade '{request.ToGrade}' does not exist.", StatusCodes.Status404NotFound);

        if (!targetGrade.IsActive)
            return ApiError($"Target grade '{request.ToGrade}' is inactive. Cannot promote students to an inactive grade.", StatusCodes.Status400BadRequest);

        var query = _context.Students
            .Where(s => s.OrganizationId == targetOrgId && s.CurrentGrade.ToLower() == request.FromGrade.Trim().ToLower() && s.Status == StudentStatus.Active && !s.IsDeleted);

        if (!string.IsNullOrWhiteSpace(request.FromSection))
            query = query.Where(s => s.Section != null && s.Section.ToLower() == request.FromSection.Trim().ToLower());

        if (request.StudentIds != null && request.StudentIds.Count > 0)
            query = query.Where(s => request.StudentIds.Contains(s.Id));

        var students = await query.ToListAsync(cancellationToken);
        if (students.Count == 0)
            return ApiError("No eligible active students found matching the promotion criteria.");

        int currentInTargetGrade = await _context.Students
            .CountAsync(s => s.OrganizationId == targetOrgId && s.CurrentGrade.ToLower() == request.ToGrade.Trim().ToLower() && s.Status == StudentStatus.Active && !s.IsDeleted, cancellationToken);

        if (currentInTargetGrade + students.Count > targetGrade.Capacity)
            return ApiError($"Promoting {students.Count} student(s) would exceed the maximum capacity ({targetGrade.Capacity}) of '{targetGrade.Name}'. Current active: {currentInTargetGrade}.", StatusCodes.Status400BadRequest);

        var studentNames = new List<string>();
        foreach (var student in students)
        {
            student.CurrentGrade = targetGrade.Name;
            if (!string.IsNullOrWhiteSpace(request.ToSection))
                student.Section = request.ToSection.Trim();

            studentNames.Add(student.FullName);
        }

        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            AuditAction.Update, "Academics", "StudentPromotion", $"{request.FromGrade}->{request.ToGrade}",
            $"Promoted {students.Count} student(s) from '{request.FromGrade}' to '{request.ToGrade}'",
            organizationId: targetOrgId,
            userId: CurrentUserService.UserId,
            userEmail: CurrentUserService.Email,
            cancellationToken: cancellationToken);

        return ApiSuccess(new PromotionResultDto(
            students.Count,
            request.FromGrade,
            request.ToGrade,
            "Promoted",
            studentNames
        ), $"{students.Count} student(s) successfully promoted to {targetGrade.Name}.");
    }

    [HttpPost("promotion/retain")]
    [RequirePermission(Permissions.AcademicsManage)]
    public async Task<IActionResult> RetainStudents([FromBody] RetainStudentsRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Grade))
            return ApiError("Grade is required.");

        Guid targetOrgId = GetTargetOrgId();
        if (targetOrgId == Guid.Empty) return ApiError("Tenant context required.", StatusCodes.Status400BadRequest);

        var query = _context.Students
            .Where(s => s.OrganizationId == targetOrgId && s.CurrentGrade.ToLower() == request.Grade.Trim().ToLower() && s.Status == StudentStatus.Active && !s.IsDeleted);

        if (!string.IsNullOrWhiteSpace(request.Section))
            query = query.Where(s => s.Section != null && s.Section.ToLower() == request.Section.Trim().ToLower());

        if (request.StudentIds != null && request.StudentIds.Count > 0)
            query = query.Where(s => request.StudentIds.Contains(s.Id));

        var students = await query.ToListAsync(cancellationToken);
        if (students.Count == 0)
            return ApiError("No eligible students found to retain.");

        var studentNames = students.Select(s => s.FullName).ToList();

        await _auditService.LogAsync(
            AuditAction.Update, "Academics", "StudentRetention", request.Grade,
            $"Retained {students.Count} student(s) in '{request.Grade}'. Reason: {request.Reason ?? "Academic requirement"}",
            organizationId: targetOrgId,
            userId: CurrentUserService.UserId,
            userEmail: CurrentUserService.Email,
            cancellationToken: cancellationToken);

        return ApiSuccess(new PromotionResultDto(
            students.Count,
            request.Grade,
            request.Grade,
            "Retained",
            studentNames
        ), $"{students.Count} student(s) retained in {request.Grade}.");
    }

    // =========================================================================
    // 8. EXAM MANAGEMENT
    // =========================================================================

    [HttpGet("exams")]
    [RequirePermission(Permissions.ExamsView)]
    public async Task<IActionResult> GetExams([FromQuery] Guid? classGradeId, CancellationToken cancellationToken)
    {
        Guid targetOrgId = GetTargetOrgId();
        if (targetOrgId == Guid.Empty) return ApiError("Tenant context required.", StatusCodes.Status400BadRequest);

        var query = _context.ExamSchedules
            .Include(e => e.ClassGrade)
            .Include(e => e.ClassSection)
            .Include(e => e.SubjectCourse)
            .Include(e => e.Results)
            .Where(e => e.OrganizationId == targetOrgId && !e.IsDeleted);

        if (classGradeId.HasValue) query = query.Where(e => e.ClassGradeId == classGradeId.Value);

        var exams = await query
            .OrderByDescending(e => e.ExamDate)
            .Select(e => new ExamScheduleDto(
                e.Id,
                e.Title,
                e.Term,
                e.ClassGradeId,
                e.ClassGrade != null ? e.ClassGrade.Name : "Class",
                e.ClassSectionId,
                e.ClassSection != null ? e.ClassSection.Name : null,
                e.SubjectCourseId,
                e.SubjectCourse != null ? e.SubjectCourse.Name : "Subject",
                e.ExamDate,
                e.StartTime,
                e.EndTime,
                e.RoomNumber,
                e.TotalMarks,
                e.PassingMarks,
                e.IsPublished,
                e.IsLocked,
                e.Results.Count(r => !r.IsDeleted)
            ))
            .ToListAsync(cancellationToken);

        return ApiSuccess(exams);
    }

    [HttpPost("exams")]
    [RequirePermission(Permissions.ExamsSchedule)]
    public async Task<IActionResult> CreateExamSchedule([FromBody] CreateExamScheduleRequest request, CancellationToken cancellationToken)
    {
        Guid targetOrgId = GetTargetOrgId();
        if (targetOrgId == Guid.Empty) return ApiError("Tenant context required.", StatusCodes.Status400BadRequest);

        var classGrade = await _context.ClassGrades.FirstOrDefaultAsync(c => c.Id == request.ClassGradeId && c.OrganizationId == targetOrgId && !c.IsDeleted, cancellationToken);
        if (classGrade == null) return ApiError("Class grade not found.");

        var subject = await _context.SubjectCourses.FirstOrDefaultAsync(s => s.Id == request.SubjectCourseId && s.OrganizationId == targetOrgId && !s.IsDeleted, cancellationToken);
        if (subject == null) return ApiError("Subject course not found.");

        var exam = new ExamSchedule
        {
            Id = Guid.NewGuid(),
            OrganizationId = targetOrgId,
            BranchId = classGrade.BranchId,
            AcademicSessionId = request.AcademicSessionId,
            ClassGradeId = request.ClassGradeId,
            ClassSectionId = request.ClassSectionId,
            SubjectCourseId = request.SubjectCourseId,
            Title = request.Title.Trim(),
            Term = request.EffectiveTerm,
            ExamDate = request.EffectiveExamDate.Date,
            StartTime = request.EffectiveStartTime,
            EndTime = request.EffectiveEndTime,
            RoomNumber = request.RoomNumber,
            TotalMarks = request.TotalMarks > 0 ? request.TotalMarks : 100.0m,
            PassingMarks = request.PassingMarks > 0 ? request.PassingMarks : 40.0m,
            IsPublished = false,
            IsLocked = false
        };

        _context.ExamSchedules.Add(exam);
        await _context.SaveChangesAsync(cancellationToken);

        return ApiSuccess(new { id = exam.Id }, "Exam schedule created successfully.");
    }

    [HttpGet("exams/{id:guid}/review")]
    [RequirePermission(Permissions.ExamsView)]
    public async Task<IActionResult> ReviewExamResults(Guid id, CancellationToken cancellationToken)
    {
        Guid targetOrgId = GetTargetOrgId();
        var exam = await _context.ExamSchedules
            .Include(e => e.ClassGrade)
            .Include(e => e.SubjectCourse)
            .Include(e => e.Results)
            .FirstOrDefaultAsync(e => e.Id == id && e.OrganizationId == targetOrgId && !e.IsDeleted, cancellationToken);

        if (exam == null) return ApiNotFound("Exam not found.");

        var activeResults = exam.Results.Where(r => !r.IsDeleted).ToList();
        int totalStudents = await _context.Students
            .CountAsync(s => s.OrganizationId == targetOrgId && s.CurrentGrade == exam.ClassGrade!.Name && s.Status == StudentStatus.Active && !s.IsDeleted, cancellationToken);

        int passed = activeResults.Count(r => r.MarksObtained >= exam.PassingMarks);
        int failed = activeResults.Count(r => r.MarksObtained < exam.PassingMarks);
        decimal highest = activeResults.Count > 0 ? activeResults.Max(r => r.MarksObtained) : 0;
        decimal lowest = activeResults.Count > 0 ? activeResults.Min(r => r.MarksObtained) : 0;
        decimal avg = activeResults.Count > 0 ? Math.Round(activeResults.Average(r => r.MarksObtained), 2) : 0;

        var review = new ExamReviewSummaryDto(
            exam.Id,
            exam.Title,
            exam.Term,
            exam.SubjectCourse?.Name ?? "Subject",
            exam.ClassGrade?.Name ?? "Class",
            exam.TotalMarks,
            totalStudents,
            activeResults.Count,
            passed,
            failed,
            highest,
            lowest,
            avg,
            exam.IsPublished,
            exam.IsLocked
        );

        return ApiSuccess(review);
    }

    [HttpPost("exams/{id:guid}/approve")]
    [RequirePermission(Permissions.ExamsPublishResults)]
    public async Task<IActionResult> ApproveExamResults(Guid id, CancellationToken cancellationToken)
    {
        Guid targetOrgId = GetTargetOrgId();
        var exam = await _context.ExamSchedules
            .FirstOrDefaultAsync(e => e.Id == id && e.OrganizationId == targetOrgId && !e.IsDeleted, cancellationToken);

        if (exam == null) return ApiNotFound("Exam not found.");

        exam.IsLocked = true;
        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            AuditAction.Update, "Exams", "ApproveExam", exam.Id.ToString(),
            $"Administrator approved marks for exam '{exam.Title}'",
            organizationId: targetOrgId,
            branchId: exam.BranchId,
            userId: CurrentUserService.UserId,
            userEmail: CurrentUserService.Email,
            cancellationToken: cancellationToken);

        return ApiSuccess("Exam marks reviewed and approved successfully.");
    }

    [HttpPost("exams/{id:guid}/publish")]
    [RequirePermission(Permissions.ExamsPublishResults)]
    public async Task<IActionResult> PublishExam(Guid id, CancellationToken cancellationToken)
    {
        Guid targetOrgId = GetTargetOrgId();
        var exam = await _context.ExamSchedules
            .Include(e => e.Results)
            .FirstOrDefaultAsync(e => e.Id == id && e.OrganizationId == targetOrgId && !e.IsDeleted, cancellationToken);

        if (exam == null) return ApiNotFound("Exam not found.");

        exam.IsPublished = true;
        exam.IsLocked = true;

        foreach (var r in exam.Results.Where(r => !r.IsDeleted))
        {
            r.IsFinalized = true;
        }

        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            AuditAction.Update, "Exams", "PublishExam", exam.Id.ToString(),
            $"Published official results for exam '{exam.Title}'",
            organizationId: targetOrgId,
            branchId: exam.BranchId,
            userId: CurrentUserService.UserId,
            userEmail: CurrentUserService.Email,
            cancellationToken: cancellationToken);

        return ApiSuccess("Exam results published successfully to students and guardians.");
    }

    [HttpPost("exams/{id:guid}/unlock")]
    [RequirePermission(Permissions.ExamsPublishResults)]
    public async Task<IActionResult> UnlockExam(Guid id, CancellationToken cancellationToken)
    {
        Guid targetOrgId = GetTargetOrgId();
        var exam = await _context.ExamSchedules
            .FirstOrDefaultAsync(e => e.Id == id && e.OrganizationId == targetOrgId && !e.IsDeleted, cancellationToken);

        if (exam == null) return ApiNotFound("Exam not found.");

        exam.IsLocked = false;
        exam.IsPublished = false;

        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            AuditAction.Update, "Exams", "UnlockExam", exam.Id.ToString(),
            $"Administrator unlocked exam '{exam.Title}' for marks correction",
            organizationId: targetOrgId,
            branchId: exam.BranchId,
            userId: CurrentUserService.UserId,
            userEmail: CurrentUserService.Email,
            cancellationToken: cancellationToken);

        return ApiSuccess("Exam unlocked successfully for marks adjustment.");
    }

    [HttpGet("exams/report-card/{studentId:guid}")]
    [RequirePermission(Permissions.ExamsView)]
    public async Task<IActionResult> GetStudentReportCard(Guid studentId, [FromQuery] string? term, CancellationToken cancellationToken)
    {
        Guid targetOrgId = GetTargetOrgId();
        var student = await _context.Students
            .FirstOrDefaultAsync(s => s.Id == studentId && s.OrganizationId == targetOrgId && !s.IsDeleted, cancellationToken);

        if (student == null) return ApiNotFound("Student not found.");

        var query = _context.ExamResults
            .Include(r => r.ExamSchedule).ThenInclude(es => es!.SubjectCourse)
            .Where(r => r.OrganizationId == targetOrgId &&
                        r.StudentId == studentId &&
                        r.ExamSchedule != null &&
                        r.ExamSchedule.IsPublished &&
                        !r.IsDeleted);

        if (!string.IsNullOrWhiteSpace(term))
            query = query.Where(r => r.ExamSchedule!.Term.ToLower() == term.Trim().ToLower());

        var results = await query.ToListAsync(cancellationToken);

        var subjects = results.Select(r => new ReportCardSubjectItemDto(
            r.ExamSchedule?.SubjectCourse?.Name ?? "Subject",
            r.ExamSchedule?.SubjectCourse?.Code ?? "SUB",
            r.ExamSchedule?.TotalMarks ?? 100,
            r.ExamSchedule?.PassingMarks ?? 40,
            r.MarksObtained,
            r.Percentage,
            r.Grade,
            r.Gpa,
            r.MarksObtained >= (r.ExamSchedule?.PassingMarks ?? 40),
            r.Remarks
        )).ToList();

        decimal totalPossible = subjects.Sum(s => s.TotalMarks);
        decimal totalObtained = subjects.Sum(s => s.MarksObtained);
        decimal overallPct = totalPossible > 0 ? Math.Round((totalObtained / totalPossible) * 100, 2) : 0;
        string overallGrade = overallPct >= 90 ? "A+" : overallPct >= 80 ? "A" : overallPct >= 70 ? "B" : overallPct >= 60 ? "C" : overallPct >= 50 ? "D" : "F";
        decimal overallGpa = subjects.Count > 0 && subjects.Any(s => s.Gpa.HasValue) ? Math.Round(subjects.Where(s => s.Gpa.HasValue).Average(s => s.Gpa!.Value), 2) : 0;
        string overallStatus = subjects.All(s => s.IsPassed) ? "Passed" : "Failed";

        var card = new StudentReportCardDto(
            student.Id,
            student.AdmissionNumber,
            student.RollNumber,
            student.FullName,
            student.CurrentGrade,
            student.Section,
            term ?? "Current Term",
            DateTime.UtcNow,
            totalPossible,
            totalObtained,
            overallPct,
            overallGrade,
            overallGpa,
            overallStatus,
            subjects
        );

        return ApiSuccess(card);
    }

    // =========================================================================
    // 9. LEAVE APPROVAL MANAGEMENT
    // =========================================================================

    [HttpGet("leaves")]
    [HttpGet("leave")]
    [RequirePermission(Permissions.AcademicsView)]
    public async Task<IActionResult> GetLeaves([FromQuery] LeaveStatus? status, CancellationToken cancellationToken)
    {
        Guid targetOrgId = GetTargetOrgId();
        if (targetOrgId == Guid.Empty) return ApiError("Tenant context required.", StatusCodes.Status400BadRequest);

        var query = _context.LeaveRequests
            .Include(l => l.RequesterUser)
            .Where(l => l.OrganizationId == targetOrgId && !l.IsDeleted);

        if (status.HasValue) query = query.Where(l => l.Status == status.Value);

        var leaves = await query
            .OrderByDescending(l => l.CreatedAtUtc)
            .Select(l => new LeaveRequestDto(
                l.Id,
                l.RequesterUser != null ? l.RequesterUser.FullName : "User",
                l.RequesterType.ToString(),
                l.LeaveType,
                l.StartDate,
                l.EndDate,
                l.Reason,
                l.Status,
                l.Status.ToString(),
                l.DecisionRemarks,
                l.DecisionDateUtc,
                l.CreatedAtUtc
            ))
            .ToListAsync(cancellationToken);

        return ApiSuccess(leaves);
    }

    [HttpPost("leaves/{id:guid}/decision")]
    [HttpPut("leaves/{id:guid}/decision")]
    [HttpPost("leave/{id:guid}/decision")]
    [HttpPut("leave/{id:guid}/decision")]
    [RequirePermission(Permissions.AcademicsManage)]
    public async Task<IActionResult> DecideLeave(Guid id, [FromBody] LeaveDecisionRequest request, CancellationToken cancellationToken)
    {
        Guid targetOrgId = GetTargetOrgId();
        var leave = await _context.LeaveRequests.FirstOrDefaultAsync(l => l.Id == id && l.OrganizationId == targetOrgId && !l.IsDeleted, cancellationToken);
        if (leave == null) return ApiNotFound("Leave request not found.");

        if (leave.Status != LeaveStatus.Pending)
            return ApiError("Only pending leave requests can be approved or rejected.");

        // Security: Prevent self-approval
        if (leave.RequesterUserId == CurrentUserService.UserId && !CurrentUserService.IsSuperAdmin)
            return ApiForbidden("You cannot approve or reject your own leave application.");

        leave.Status = request.Status;
        leave.DecisionRemarks = request.DecisionRemarks;
        leave.DecisionDateUtc = DateTime.UtcNow;
        leave.ApprovedByUserId = CurrentUserService.UserId;

        if (request.Status == LeaveStatus.Approved && leave.StudentId.HasValue)
        {
            var student = await _context.Students.FirstOrDefaultAsync(s => s.Id == leave.StudentId.Value && !s.IsDeleted, cancellationToken);
            Guid? classGradeId = null;
            if (student != null)
            {
                var cg = await _context.ClassGrades.FirstOrDefaultAsync(c => c.OrganizationId == targetOrgId && c.Name.ToLower() == student.CurrentGrade.ToLower() && !c.IsDeleted, cancellationToken);
                classGradeId = cg?.Id;
            }

            for (var d = leave.StartDate.Date; d <= leave.EndDate.Date; d = d.AddDays(1))
            {
                var existingAttendance = await _context.AttendanceRecords
                    .FirstOrDefaultAsync(a => a.OrganizationId == targetOrgId && a.StudentId == leave.StudentId.Value && a.Date == d && !a.IsDeleted, cancellationToken);

                if (existingAttendance != null)
                {
                    existingAttendance.Status = AttendanceStatus.Leave;
                    existingAttendance.Remarks = $"[Approved Leave]: {leave.Reason}";
                    existingAttendance.UpdatedAtUtc = DateTime.UtcNow;
                }
                else
                {
                    var att = new AttendanceRecord
                    {
                        Id = Guid.NewGuid(),
                        OrganizationId = targetOrgId,
                        BranchId = leave.BranchId,
                        StudentId = leave.StudentId.Value,
                        ClassGradeId = classGradeId ?? Guid.Empty,
                        Date = d,
                        Status = AttendanceStatus.Leave,
                        Remarks = $"[Approved Leave]: {leave.Reason}",
                        IsLocked = false
                    };
                    _context.AttendanceRecords.Add(att);
                }
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        return ApiSuccess($"Leave request marked as {request.Status}.");
    }

    // =========================================================================
    // 10. FEES INVOICE & PAYMENT MANAGEMENT
    // =========================================================================

    [HttpPost("fees/invoices")]
    [RequirePermission(Permissions.FeesGenerateInvoices)]
    public async Task<IActionResult> CreateFeeInvoice([FromBody] CreateFeeInvoiceRequest request, CancellationToken cancellationToken)
    {
        Guid targetOrgId = GetTargetOrgId();
        if (targetOrgId == Guid.Empty) return ApiError("Tenant context required.", StatusCodes.Status400BadRequest);

        var student = await _context.Students.FirstOrDefaultAsync(s => s.Id == request.StudentId && s.OrganizationId == targetOrgId && !s.IsDeleted, cancellationToken);
        if (student == null) return ApiNotFound("Student not found.");

        string invoiceNum = $"INV-{DateTime.UtcNow:yyyyMM}-{Guid.NewGuid().ToString()[..6].ToUpper()}";
        var invoice = new FeeInvoice
        {
            Id = Guid.NewGuid(),
            OrganizationId = targetOrgId,
            BranchId = request.BranchId ?? student.BranchId,
            StudentId = request.StudentId,
            InvoiceNumber = invoiceNum,
            Title = request.Title ?? "Monthly Tuition Fee",
            IssueDate = DateTime.UtcNow.Date,
            DueDate = request.DueDate.Date,
            TotalAmount = request.TotalAmount,
            PaidAmount = 0.0m,
            Status = FeeStatus.Unpaid
        };

        _context.FeeInvoices.Add(invoice);
        await _context.SaveChangesAsync(cancellationToken);

        return ApiSuccess(new { id = invoice.Id, invoiceNumber = invoice.InvoiceNumber }, "Fee invoice generated successfully.");
    }

    [HttpPost("fees/invoices/{id:guid}/payments")]
    [RequirePermission(Permissions.FeesCollectPayment)]
    public async Task<IActionResult> RecordFeePayment(Guid id, [FromBody] RecordFeePaymentRequest request, CancellationToken cancellationToken)
    {
        Guid targetOrgId = GetTargetOrgId();
        var invoice = await _context.FeeInvoices.FirstOrDefaultAsync(f => f.Id == id && f.OrganizationId == targetOrgId && !f.IsDeleted, cancellationToken);
        if (invoice == null) return ApiNotFound("Fee invoice not found.");

        if (request.AmountPaid <= 0) return ApiError("Payment amount must be greater than zero.");

        decimal newPaid = invoice.PaidAmount + request.AmountPaid;
        invoice.PaidAmount = newPaid;
        invoice.Status = newPaid >= invoice.TotalAmount ? FeeStatus.Paid : FeeStatus.PartiallyPaid;

        string receiptNum = $"REC-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..6].ToUpper()}";
        var payment = new FeePayment
        {
            Id = Guid.NewGuid(),
            OrganizationId = targetOrgId,
            BranchId = invoice.BranchId,
            FeeInvoiceId = invoice.Id,
            StudentId = invoice.StudentId,
            ReceiptNumber = receiptNum,
            AmountPaid = request.AmountPaid,
            PaymentDate = DateTime.UtcNow,
            PaymentMethod = request.PaymentMethod,
            TransactionReference = request.TransactionReference,
            ReceivedByUserId = CurrentUserService.UserId
        };

        _context.FeePayments.Add(payment);
        await _context.SaveChangesAsync(cancellationToken);

        return ApiSuccess(new { receiptNumber = receiptNum, remainingBalance = invoice.Balance }, "Fee payment recorded successfully.");
    }
}
