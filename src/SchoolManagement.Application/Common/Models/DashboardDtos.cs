namespace SchoolManagement.Application.Common.Models;

public record RecentActivityDto(
    Guid Id,
    string Action,
    string Module,
    string EntityName,
    string? Details,
    string? UserEmail,
    DateTime TimestampUtc
);

public record DashboardSummaryDto(
    bool IsSuperAdmin,
    Guid? OrganizationId,
    string? OrganizationName,
    Guid? BranchId,
    string? BranchName,
    int TotalOrganizations,
    int TotalBranches,
    int TotalUsers,
    int TotalStaff,
    int TotalStudents,
    IReadOnlyList<RecentActivityDto> RecentActivities,
    int TotalClasses = 0,
    int TotalDepartments = 0
);
