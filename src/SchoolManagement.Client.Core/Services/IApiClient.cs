using SchoolManagement.Application.Common.Models;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Client.Core.Services;

public interface IApiClient
{
    void SetAuthToken(string? token);
    Task<AuthResultDto> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default);
    Task<SyncPushResponseDto> PushSyncBatchAsync(SyncPushRequestDto request, CancellationToken cancellationToken = default);
    Task<SyncPullResponseDto> PullSyncDeltaAsync(Guid organizationId, long sinceVersion, CancellationToken cancellationToken = default);
    Task<bool> CheckServerHealthAsync(CancellationToken cancellationToken = default);
    Task<(bool Success, string Message)> ChangePasswordAsync(ChangePasswordRequestDto request, CancellationToken cancellationToken = default);
    Task<(bool Success, string Message)> ForgotPasswordAsync(ForgotPasswordRequestDto request, CancellationToken cancellationToken = default);
    Task<(bool Success, string Message)> ResetPasswordAsync(ResetPasswordRequestDto request, CancellationToken cancellationToken = default);
    Task<UserDto?> GetProfileAsync(CancellationToken cancellationToken = default);
    Task<(bool Success, string Message)> UpdateProfileAsync(UpdateProfileRequestDto request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OrganizationSummaryDto>> GetOrganizationsAsync(string? search = null, bool? isActive = null, CancellationToken cancellationToken = default);
    Task<OrganizationDetailsDto?> GetOrganizationByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<(bool Success, string Message)> CreateOrganizationAsync(CreateOrganizationRequestDto request, CancellationToken cancellationToken = default);
    Task<(bool Success, string Message)> UpdateOrganizationAsync(Guid id, UpdateOrganizationRequestDto request, CancellationToken cancellationToken = default);
    Task<(bool Success, string Message)> ToggleOrganizationStatusAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BranchDto>> GetBranchesAsync(Guid? organizationId = null, bool? isActive = null, CancellationToken cancellationToken = default);
    Task<(bool Success, string Message)> CreateBranchAsync(CreateBranchRequestDto request, CancellationToken cancellationToken = default);
    Task<(bool Success, string Message)> UpdateBranchAsync(Guid id, UpdateBranchRequestDto request, CancellationToken cancellationToken = default);
    Task<(bool Success, string Message)> DeleteBranchAsync(Guid id, CancellationToken cancellationToken = default);
    Task<AuthResultDto> SwitchBranchAsync(Guid branchId, CancellationToken cancellationToken = default);
    Task<SchoolProfileDto?> GetMySchoolAsync(CancellationToken cancellationToken = default);
    Task<(bool Success, string Message)> UpdateMySchoolAsync(UpdateSchoolProfileRequestDto request, CancellationToken cancellationToken = default);
    Task<DashboardSummaryDto?> GetDashboardSummaryAsync(CancellationToken cancellationToken = default);

    // Phase 7: Student Management
    Task<IReadOnlyList<StudentSummaryDto>> GetStudentsAsync(string? search = null, string? grade = null, string? section = null, StudentStatus? status = null, Guid? branchId = null, int pageNumber = 1, int pageSize = 50, CancellationToken cancellationToken = default);
    Task<StudentDetailsDto?> GetStudentByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<(bool Success, string Message, Guid? StudentId)> CreateStudentAsync(CreateStudentRequestDto request, CancellationToken cancellationToken = default);
    Task<(bool Success, string Message)> UpdateStudentAsync(Guid id, UpdateStudentRequestDto request, CancellationToken cancellationToken = default);
    Task<(bool Success, string Message)> DeleteStudentAsync(Guid id, CancellationToken cancellationToken = default);
    Task<(bool Success, string Message)> AssignGuardianAsync(Guid studentId, AssignGuardianRequestDto request, CancellationToken cancellationToken = default);
    Task<(bool Success, string Message)> UnlinkGuardianAsync(Guid studentId, Guid guardianId, CancellationToken cancellationToken = default);

    // Phase 7: Parent & Guardian Management
    Task<IReadOnlyList<GuardianSummaryDto>> GetGuardiansAsync(string? search = null, Guid? branchId = null, int pageNumber = 1, int pageSize = 50, CancellationToken cancellationToken = default);
    Task<GuardianDetailsDto?> GetGuardianByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<(bool Success, string Message, Guid? GuardianId)> CreateGuardianAsync(CreateGuardianRequestDto request, CancellationToken cancellationToken = default);
    Task<(bool Success, string Message)> UpdateGuardianAsync(Guid id, UpdateGuardianRequestDto request, CancellationToken cancellationToken = default);
    Task<(bool Success, string Message)> DeleteGuardianAsync(Guid id, CancellationToken cancellationToken = default);

    // Phase 7: Staff & Teacher Management
    Task<IReadOnlyList<StaffSummaryDto>> GetStaffAsync(string? search = null, string? department = null, string? designation = null, EmploymentType? employmentType = null, StaffStatus? status = null, Guid? branchId = null, int pageNumber = 1, int pageSize = 50, CancellationToken cancellationToken = default);
    Task<StaffDetailsDto?> GetStaffByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<(bool Success, string Message, Guid? StaffId)> CreateStaffAsync(CreateStaffRequestDto request, CancellationToken cancellationToken = default);
    Task<(bool Success, string Message)> UpdateStaffAsync(Guid id, UpdateStaffRequestDto request, CancellationToken cancellationToken = default);
    Task<(bool Success, string Message)> ToggleStaffStatusAsync(Guid id, StaffStatus newStatus, CancellationToken cancellationToken = default);
    Task<(bool Success, string Message)> DeleteStaffAsync(Guid id, CancellationToken cancellationToken = default);
}
