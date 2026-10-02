namespace SchoolManagement.Application.Common.Models;

public record OrganizationProfileDto(
    Guid Id,
    string Name,
    string Code,
    string? LogoUrl,
    string Email,
    string Phone,
    string Address,
    string ThemePrimaryColor,
    string ThemeSecondaryColor,
    bool ThemeDarkMode,
    int MaxBranches,
    int CurrentActiveBranches,
    string? Website,
    string? PrincipalName,
    string? RegistrationNumber,
    string? OrganizationType,
    string? Description,
    string? WorkingDays,
    string? WorkingHours,
    string? TimeZone,
    string? Currency,
    string? DateFormat,
    string? Language,
    IReadOnlyList<string> EnabledModules
);

public record UpdateOrganizationProfileRequest(
    string Name,
    string? LogoUrl = null,
    string? Email = null,
    string? Phone = null,
    string? Address = null,
    string? ThemePrimaryColor = null,
    string? ThemeSecondaryColor = null,
    bool ThemeDarkMode = true,
    string? Website = null,
    string? PrincipalName = null,
    string? RegistrationNumber = null,
    string? OrganizationType = null,
    string? Description = null,
    string? WorkingDays = null,
    string? WorkingHours = null,
    string? TimeZone = null,
    string? Currency = null,
    string? DateFormat = null,
    string? Language = null
);

public record OrganizationSettingDto(
    Guid Id,
    Guid OrganizationId,
    string Currency,
    string DateFormat,
    string TimeFormat,
    string TimeZone,
    string Language,
    string GradingSystem,
    decimal PassingMarksPercentage,
    string WorkingDays,
    string WorkingHours,
    string StudentIdFormat,
    string EmployeeIdFormat,
    decimal AttendanceThresholdPercentage
);

public record UpdateOrganizationSettingRequest(
    string? Currency = null,
    string? DateFormat = null,
    string? TimeFormat = null,
    string? TimeZone = null,
    string? Language = null,
    string? GradingSystem = null,
    decimal? PassingMarksPercentage = null,
    string? WorkingDays = null,
    string? WorkingHours = null,
    string? StudentIdFormat = null,
    string? EmployeeIdFormat = null,
    decimal? AttendanceThresholdPercentage = null
);

public record NotificationSettingDto(
    Guid Id,
    Guid OrganizationId,
    bool EmailEnabled,
    bool SmsEnabled,
    bool WhatsAppEnabled,
    bool PushEnabled,
    bool NotifyOnAdmission,
    bool NotifyOnFeeDue,
    bool NotifyOnFeePayment,
    bool NotifyOnAttendanceAbsence,
    bool NotifyOnExamResult,
    bool NotifyOnAnnouncement
);

public record UpdateNotificationSettingRequest(
    bool EmailEnabled,
    bool SmsEnabled,
    bool WhatsAppEnabled,
    bool PushEnabled,
    bool NotifyOnAdmission,
    bool NotifyOnFeeDue,
    bool NotifyOnFeePayment,
    bool NotifyOnAttendanceAbsence,
    bool NotifyOnExamResult,
    bool NotifyOnAnnouncement
);

public record AnnouncementDto(
    Guid Id,
    Guid OrganizationId,
    Guid? BranchId,
    string? BranchName,
    string Title,
    string Content,
    string TargetAudience,
    Guid? ClassGradeId,
    string? ClassGradeName,
    Guid? DepartmentId,
    string? DepartmentName,
    string Priority,
    DateTime PublishDate,
    DateTime? ExpiryDate,
    bool IsActive
);

public record CreateAnnouncementRequest(
    string Title,
    string Content,
    string TargetAudience = "All",
    Guid? BranchId = null,
    Guid? ClassGradeId = null,
    Guid? DepartmentId = null,
    string Priority = "Normal",
    DateTime? ExpiryDate = null
);

public record UpdateAnnouncementRequest(
    string Title,
    string Content,
    string TargetAudience,
    Guid? ClassGradeId,
    Guid? DepartmentId,
    string Priority,
    DateTime? ExpiryDate,
    bool IsActive
);

public record CommunicationTemplateDto(
    string Key,
    string Title,
    string DefaultContent,
    string TargetAudience,
    string Priority,
    string Category
);

public record DispatchNotificationRequest(
    string Title,
    string Message,
    string Channel = "InApp", // InApp, Email, SMS, WhatsApp
    string TargetAudience = "All", // All, Teachers, Students, Parents, Staff
    Guid? BranchId = null,
    string? TemplateCode = null
);

public record CommunicationLogDto(
    Guid Id,
    Guid OrganizationId,
    Guid? BranchId,
    string? BranchName,
    string Title,
    string Message,
    string Channel,
    string TargetAudience,
    int RecipientCount,
    string Status,
    string? FailureReason,
    string? SentByUserEmail,
    DateTime CreatedAtUtc
);

