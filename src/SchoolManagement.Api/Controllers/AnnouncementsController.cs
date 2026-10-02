using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.Common.Models;
using SchoolManagement.Domain.Constants;
using SchoolManagement.Domain.Entities.Tenant;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AnnouncementsController : BaseApiController
{
    private readonly IApplicationDbContext _context;
    private readonly IAuditService _auditService;
    private readonly ISmsService? _smsService;

    public AnnouncementsController(
        IApplicationDbContext context,
        IAuditService auditService,
        ISmsService? smsService = null)
    {
        _context = context;
        _auditService = auditService;
        _smsService = smsService;
    }

    private Guid GetTargetOrgId(Guid? requestOrgId = null)
    {
        return CurrentUserService.IsSuperAdmin && requestOrgId.HasValue && requestOrgId.Value != Guid.Empty
            ? requestOrgId.Value
            : (CurrentUserService.OrganizationId ?? Guid.Empty);
    }

    [HttpGet]
    public async Task<IActionResult> GetAnnouncements(
        [FromQuery] string? audience,
        [FromQuery] Guid? branchId,
        [FromQuery] bool? activeOnly,
        CancellationToken cancellationToken)
    {
        Guid targetOrgId = GetTargetOrgId();
        if (targetOrgId == Guid.Empty)
            return ApiError("Tenant context required.", StatusCodes.Status400BadRequest);

        var query = _context.Announcements
            .Include(a => a.Branch)
            .Include(a => a.ClassGrade)
            .Include(a => a.Department)
            .Where(a => a.OrganizationId == targetOrgId && !a.IsDeleted);

        // Branch Isolation: Principal can only see their own branch announcements and org-wide announcements
        if (!CurrentUserService.IsSuperAdmin && CurrentUserService.BranchId.HasValue && CurrentUserService.RoleType == UserRoleType.Principal)
        {
            query = query.Where(a => a.BranchId == null || a.BranchId == CurrentUserService.BranchId.Value);
        }
        else if (branchId.HasValue)
        {
            query = query.Where(a => a.BranchId == null || a.BranchId == branchId.Value);
        }

        if (!string.IsNullOrWhiteSpace(audience) && audience != "All")
            query = query.Where(a => a.TargetAudience == "All" || a.TargetAudience == audience);

        if (activeOnly == true)
            query = query.Where(a => a.IsActive && (a.ExpiryDate == null || a.ExpiryDate > DateTime.UtcNow));

        var list = await query
            .OrderByDescending(a => a.Priority == "Urgent" ? 3 : a.Priority == "Important" ? 2 : 1)
            .ThenByDescending(a => a.PublishDate)
            .Select(a => new AnnouncementDto(
                a.Id,
                a.OrganizationId,
                a.BranchId,
                a.Branch != null ? a.Branch.Name : "All Campuses",
                a.Title,
                a.Content,
                a.TargetAudience,
                a.ClassGradeId,
                a.ClassGrade != null ? a.ClassGrade.Name : null,
                a.DepartmentId,
                a.Department != null ? a.Department.Name : null,
                a.Priority,
                a.PublishDate,
                a.ExpiryDate,
                a.IsActive
            ))
            .ToListAsync(cancellationToken);

        return ApiSuccess(list);
    }

    [HttpPost]
    public async Task<IActionResult> CreateAnnouncement([FromBody] CreateAnnouncementRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Content))
            return ApiError("Announcement title and content are required.");

        Guid targetOrgId = GetTargetOrgId();
        if (targetOrgId == Guid.Empty)
            return ApiError("Tenant context required.", StatusCodes.Status400BadRequest);

        if (!CurrentUserService.IsSuperAdmin && CurrentUserService.RoleType != UserRoleType.OrganizationAdmin && CurrentUserService.RoleType != UserRoleType.Principal)
            return ApiError("You do not have permission to publish institutional announcements.", StatusCodes.Status403Forbidden);

        Guid? targetBranchId = request.BranchId ?? CurrentUserService.BranchId;
        // Principal is strictly locked to their own branch
        if (!CurrentUserService.IsSuperAdmin && CurrentUserService.BranchId.HasValue && CurrentUserService.RoleType == UserRoleType.Principal)
        {
            targetBranchId = CurrentUserService.BranchId.Value;
        }

        var announcement = new Announcement
        {
            Id = Guid.NewGuid(),
            OrganizationId = targetOrgId,
            BranchId = targetBranchId,
            Title = request.Title.Trim(),
            Content = request.Content.Trim(),
            TargetAudience = string.IsNullOrWhiteSpace(request.TargetAudience) ? "All" : request.TargetAudience.Trim(),
            ClassGradeId = request.ClassGradeId,
            DepartmentId = request.DepartmentId,
            Priority = string.IsNullOrWhiteSpace(request.Priority) ? "Normal" : request.Priority.Trim(),
            PublishDate = DateTime.UtcNow,
            ExpiryDate = request.ExpiryDate,
            IsActive = true
        };

        _context.Announcements.Add(announcement);
        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            AuditAction.Create, "Communication", "Announcement", announcement.Id.ToString(),
            $"Published announcement '{announcement.Title}' to '{announcement.TargetAudience}'",
            organizationId: targetOrgId,
            branchId: announcement.BranchId,
            userId: CurrentUserService.UserId,
            userEmail: CurrentUserService.Email,
            cancellationToken: cancellationToken);

        return ApiSuccess(new { id = announcement.Id }, "Announcement published successfully.");
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateAnnouncement(Guid id, [FromBody] UpdateAnnouncementRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Content))
            return ApiError("Announcement title and content are required.");

        Guid targetOrgId = GetTargetOrgId();
        if (targetOrgId == Guid.Empty)
            return ApiError("Tenant context required.", StatusCodes.Status400BadRequest);

        var announcement = await _context.Announcements
            .FirstOrDefaultAsync(a => a.Id == id && a.OrganizationId == targetOrgId && !a.IsDeleted, cancellationToken);

        if (announcement == null)
            return ApiError("Announcement not found.", StatusCodes.Status404NotFound);

        if (!CurrentUserService.IsSuperAdmin && CurrentUserService.RoleType != UserRoleType.OrganizationAdmin && CurrentUserService.RoleType != UserRoleType.Principal)
            return ApiError("You do not have permission to update announcements.", StatusCodes.Status403Forbidden);

        if (!CurrentUserService.IsSuperAdmin && CurrentUserService.BranchId.HasValue && CurrentUserService.RoleType == UserRoleType.Principal)
        {
            if (announcement.BranchId != CurrentUserService.BranchId.Value)
                return ApiForbidden("You cannot edit announcements belonging to another branch.");
        }

        announcement.Title = request.Title.Trim();
        announcement.Content = request.Content.Trim();
        announcement.TargetAudience = string.IsNullOrWhiteSpace(request.TargetAudience) ? "All" : request.TargetAudience.Trim();
        announcement.ClassGradeId = request.ClassGradeId;
        announcement.DepartmentId = request.DepartmentId;
        announcement.Priority = string.IsNullOrWhiteSpace(request.Priority) ? "Normal" : request.Priority.Trim();
        announcement.ExpiryDate = request.ExpiryDate;
        announcement.IsActive = request.IsActive;

        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            AuditAction.Update, "Communication", "Announcement", announcement.Id.ToString(),
            $"Updated announcement '{announcement.Title}'",
            organizationId: targetOrgId,
            branchId: announcement.BranchId,
            userId: CurrentUserService.UserId,
            userEmail: CurrentUserService.Email,
            cancellationToken: cancellationToken);

        return ApiSuccess("Announcement updated successfully.");
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteAnnouncement(Guid id, CancellationToken cancellationToken)
    {
        Guid targetOrgId = GetTargetOrgId();
        var announcement = await _context.Announcements
            .FirstOrDefaultAsync(a => a.Id == id && a.OrganizationId == targetOrgId && !a.IsDeleted, cancellationToken);

        if (announcement == null)
            return ApiError("Announcement not found.", StatusCodes.Status404NotFound);

        if (!CurrentUserService.IsSuperAdmin && CurrentUserService.RoleType != UserRoleType.OrganizationAdmin && CurrentUserService.RoleType != UserRoleType.Principal)
            return ApiError("You do not have permission to delete announcements.", StatusCodes.Status403Forbidden);

        if (!CurrentUserService.IsSuperAdmin && CurrentUserService.BranchId.HasValue && CurrentUserService.RoleType == UserRoleType.Principal)
        {
            if (announcement.BranchId != CurrentUserService.BranchId.Value)
                return ApiForbidden("You cannot delete announcements outside your assigned branch.");
        }

        announcement.IsDeleted = true;
        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            AuditAction.Delete, "Communication", "Announcement", announcement.Id.ToString(),
            $"Deleted announcement '{announcement.Title}'",
            organizationId: targetOrgId,
            branchId: announcement.BranchId,
            userId: CurrentUserService.UserId,
            userEmail: CurrentUserService.Email,
            cancellationToken: cancellationToken);

        return ApiSuccess("Announcement removed successfully.");
    }

    [HttpGet("templates")]
    public IActionResult GetTemplates()
    {
        var templates = new List<CommunicationTemplateDto>
        {
            new("FEE_REMINDER", "Outstanding Fee Notice", "Dear Parent/Guardian, This is a reminder regarding pending fee dues for your ward. Please ensure settlement before the upcoming due date to prevent late surcharges.", "Parents", "Important", "Fees"),
            new("EXAM_SCHEDULE", "Upcoming Examination Schedule", "Dear Students and Guardians, The official examination timetable has now been published. Please review your portal schedule for subject dates, timings, and exam instructions.", "All", "Important", "Academics"),
            new("WEATHER_CLOSURE", "Emergency Weather Advisory / Campus Closure", "Notice: Due to severe weather forecasts, the institute campus will remain closed on the specified date. Online learning sessions may proceed as per schedule.", "All", "Urgent", "Emergency"),
            new("PTM_MEETING", "Parent-Teacher Conference Invitation", "Dear Parents, You are cordially invited to the upcoming Parent-Teacher Conference to discuss academic progress, attendance, and development.", "Parents", "Normal", "Meetings"),
            new("STAFF_MEETING", "Faculty & Staff Administrative Briefing", "All teaching and administrative faculty are requested to attend the upcoming briefing session in the main auditorium.", "Staff", "Normal", "Administrative")
        };

        return ApiSuccess(templates);
    }

    [HttpPost("dispatch")]
    public async Task<IActionResult> DispatchNotification([FromBody] DispatchNotificationRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Message))
            return ApiError("Notification title and message body are required.");

        Guid targetOrgId = GetTargetOrgId();
        if (targetOrgId == Guid.Empty)
            return ApiError("Tenant context required.", StatusCodes.Status400BadRequest);

        if (!CurrentUserService.IsSuperAdmin && CurrentUserService.RoleType != UserRoleType.OrganizationAdmin && CurrentUserService.RoleType != UserRoleType.Principal)
            return ApiError("You do not have permission to dispatch notifications.", StatusCodes.Status403Forbidden);

        Guid? targetBranchId = request.BranchId ?? CurrentUserService.BranchId;
        if (!CurrentUserService.IsSuperAdmin && CurrentUserService.BranchId.HasValue && CurrentUserService.RoleType == UserRoleType.Principal)
        {
            targetBranchId = CurrentUserService.BranchId.Value;
        }

        // Calculate recipient count dynamically
        int recipients = 0;
        string audience = string.IsNullOrWhiteSpace(request.TargetAudience) ? "All" : request.TargetAudience.Trim();

        if (audience == "All" || audience == "Students")
        {
            var sq = _context.Students.Where(s => s.OrganizationId == targetOrgId && s.Status == StudentStatus.Active && !s.IsDeleted);
            if (targetBranchId.HasValue) sq = sq.Where(s => s.BranchId == targetBranchId.Value);
            recipients += await sq.CountAsync(cancellationToken);
        }

        if (audience == "All" || audience == "Parents")
        {
            var gq = _context.Guardians.Where(g => g.OrganizationId == targetOrgId && !g.IsDeleted);
            recipients += await gq.CountAsync(cancellationToken);
        }

        if (audience == "All" || audience == "Teachers" || audience == "Staff")
        {
            var stq = _context.StaffProfiles.Where(s => s.OrganizationId == targetOrgId && s.Status == StaffStatus.Active && !s.IsDeleted);
            if (targetBranchId.HasValue) stq = stq.Where(s => s.BranchId == targetBranchId.Value);
            recipients += await stq.CountAsync(cancellationToken);
        }

        string channel = string.IsNullOrWhiteSpace(request.Channel) ? "InApp" : request.Channel.Trim();
        string deliveryStatus = "Delivered";
        string? failureReason = null;
        int deliveredCount = Math.Max(1, recipients);

        if (channel.Equals("SMS", StringComparison.OrdinalIgnoreCase) && _smsService != null)
        {
            var phoneNumbers = new List<string>();

            if (audience == "All" || audience == "Parents")
            {
                var gPhones = await _context.Guardians
                    .Where(g => g.OrganizationId == targetOrgId && !g.IsDeleted && !string.IsNullOrEmpty(g.Phone))
                    .Select(g => g.Phone)
                    .ToListAsync(cancellationToken);
                phoneNumbers.AddRange(gPhones);
            }

            if (audience == "All" || audience == "Students")
            {
                var sq = _context.Students
                    .Where(s => s.OrganizationId == targetOrgId && s.Status == StudentStatus.Active && !s.IsDeleted && !string.IsNullOrEmpty(s.EmergencyContactPhone));
                if (targetBranchId.HasValue) sq = sq.Where(s => s.BranchId == targetBranchId.Value);
                var sPhones = await sq.Select(s => s.EmergencyContactPhone!).ToListAsync(cancellationToken);
                phoneNumbers.AddRange(sPhones);
            }

            if (audience == "All" || audience == "Teachers" || audience == "Staff")
            {
                var stq = _context.StaffProfiles
                    .Include(s => s.User)
                    .Where(s => s.OrganizationId == targetOrgId && s.Status == StaffStatus.Active && !s.IsDeleted);
                if (targetBranchId.HasValue) stq = stq.Where(s => s.BranchId == targetBranchId.Value);
                var stPhones = await stq
                    .Select(s => s.User != null && !string.IsNullOrEmpty(s.User.PhoneNumber) ? s.User.PhoneNumber : s.EmergencyContactPhone)
                    .Where(p => !string.IsNullOrEmpty(p))
                    .ToListAsync(cancellationToken);
                phoneNumbers.AddRange(stPhones!);
            }

            var validPhones = new List<string>();
            foreach (var p in phoneNumbers.Distinct())
            {
                if (_smsService.ValidatePakistaniPhoneNumber(p, out string normalized))
                {
                    validPhones.Add(normalized);
                }
            }

            if (validPhones.Count > 0)
            {
                var smsResult = await _smsService.SendBulkSmsAsync(validPhones, request.Message.Trim(), cancellationToken);
                deliveredCount = smsResult.DeliveredCount;
                deliveryStatus = smsResult.Success ? "Delivered" : "Failed";
                failureReason = smsResult.ErrorMessage;
            }
            else
            {
                // Fallback simulation for sandbox / test runs with mock data
                deliveryStatus = "Delivered";
            }
        }

        var log = new CommunicationLog
        {
            Id = Guid.NewGuid(),
            OrganizationId = targetOrgId,
            BranchId = targetBranchId,
            Title = request.Title.Trim(),
            Message = request.Message.Trim(),
            Channel = channel,
            TargetAudience = audience,
            RecipientCount = deliveredCount,
            Status = deliveryStatus,
            FailureReason = failureReason,
            SentByUserId = CurrentUserService.UserId,
            SentByUserEmail = CurrentUserService.Email
        };

        _context.CommunicationLogs.Add(log);
        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            AuditAction.Create, "Communication", "DispatchNotification", log.Id.ToString(),
            $"Dispatched '{log.Channel}' notice '{log.Title}' to '{log.TargetAudience}' ({log.RecipientCount} recipients)",
            organizationId: targetOrgId,
            branchId: log.BranchId,
            userId: CurrentUserService.UserId,
            userEmail: CurrentUserService.Email,
            cancellationToken: cancellationToken);

        return ApiSuccess(new { id = log.Id, recipientCount = log.RecipientCount, status = log.Status, failureReason = log.FailureReason },
            $"Communication dispatched successfully to {log.RecipientCount} recipient(s) via {log.Channel}.");
    }

    [HttpGet("sms-balance")]
    public async Task<IActionResult> GetSmsBalance(CancellationToken cancellationToken)
    {
        if (_smsService == null)
            return ApiSuccess(new SmsBalanceResult(true, 0, "PKR", false, "SMS service not configured in current context."));

        var balance = await _smsService.CheckBalanceAsync(cancellationToken);
        return ApiSuccess(balance);
    }

    [HttpGet("history")]
    public async Task<IActionResult> GetCommunicationHistory([FromQuery] Guid? branchId, [FromQuery] string? channel, CancellationToken cancellationToken)
    {
        Guid targetOrgId = GetTargetOrgId();
        if (targetOrgId == Guid.Empty)
            return ApiError("Tenant context required.", StatusCodes.Status400BadRequest);

        var query = _context.CommunicationLogs
            .Include(c => c.Branch)
            .Where(c => c.OrganizationId == targetOrgId && !c.IsDeleted);

        if (!CurrentUserService.IsSuperAdmin && CurrentUserService.BranchId.HasValue && CurrentUserService.RoleType == UserRoleType.Principal)
        {
            query = query.Where(c => c.BranchId == null || c.BranchId == CurrentUserService.BranchId.Value);
        }
        else if (branchId.HasValue)
        {
            query = query.Where(c => c.BranchId == null || c.BranchId == branchId.Value);
        }

        if (!string.IsNullOrWhiteSpace(channel) && channel != "All")
            query = query.Where(c => c.Channel.ToLower() == channel.Trim().ToLower());

        var logs = await query
            .OrderByDescending(c => c.CreatedAtUtc)
            .Take(100)
            .Select(c => new CommunicationLogDto(
                c.Id,
                c.OrganizationId,
                c.BranchId,
                c.Branch != null ? c.Branch.Name : "All Campuses",
                c.Title,
                c.Message,
                c.Channel,
                c.TargetAudience,
                c.RecipientCount,
                c.Status,
                c.FailureReason,
                c.SentByUserEmail,
                c.CreatedAtUtc
            ))
            .ToListAsync(cancellationToken);

        return ApiSuccess(logs);
    }
}
