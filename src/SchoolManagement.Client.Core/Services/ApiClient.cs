using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using SchoolManagement.Application.Common.Models;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Client.Core.Services;

public class ApiClient : IApiClient
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;

    public ApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public void SetAuthToken(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            _httpClient.DefaultRequestHeaders.Authorization = null;
        }
        else
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }

    public async Task<AuthResultDto> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/auth/login", request, _jsonOptions, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                return AuthResultDto.Failed($"Login failed ({response.StatusCode}): {errorContent}");
            }

            var result = await response.Content.ReadFromJsonAsync<ApiResponseEnvelope<AuthResultDto>>(_jsonOptions, cancellationToken);
            return result?.Data ?? AuthResultDto.Failed("Empty response from server.");
        }
        catch (Exception ex)
        {
            return AuthResultDto.Failed($"Network error: {ex.Message}");
        }
    }

    public async Task<SyncPushResponseDto> PushSyncBatchAsync(SyncPushRequestDto request, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/sync/push", request, _jsonOptions, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new SyncPushResponseDto(false, Array.Empty<SyncItemAckDto>(), $"Push failed ({response.StatusCode})");
            }

            var result = await response.Content.ReadFromJsonAsync<ApiResponseEnvelope<SyncPushResponseDto>>(_jsonOptions, cancellationToken);
            return result?.Data ?? new SyncPushResponseDto(false, Array.Empty<SyncItemAckDto>(), "Empty server response");
        }
        catch (Exception ex)
        {
            return new SyncPushResponseDto(false, Array.Empty<SyncItemAckDto>(), $"Network failure: {ex.Message}");
        }
    }

    public async Task<SyncPullResponseDto> PullSyncDeltaAsync(Guid organizationId, long sinceVersion, CancellationToken cancellationToken = default)
    {
        try
        {
            string url = $"api/sync/pull?organizationId={organizationId}&sinceVersion={sinceVersion}";
            var response = await _httpClient.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new SyncPullResponseDto(false, sinceVersion, Array.Empty<SyncPullItemDto>(), $"Pull failed ({response.StatusCode})");
            }

            var result = await response.Content.ReadFromJsonAsync<ApiResponseEnvelope<SyncPullResponseDto>>(_jsonOptions, cancellationToken);
            return result?.Data ?? new SyncPullResponseDto(false, sinceVersion, Array.Empty<SyncPullItemDto>(), "Empty server response");
        }
        catch (Exception ex)
        {
            return new SyncPullResponseDto(false, sinceVersion, Array.Empty<SyncPullItemDto>(), $"Network failure: {ex.Message}");
        }
    }

    public async Task<bool> CheckServerHealthAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("api/health", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<(bool Success, string Message)> ChangePasswordAsync(ChangePasswordRequestDto request, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/auth/change-password", request, _jsonOptions, cancellationToken);
            var envelope = await response.Content.ReadFromJsonAsync<ApiResponseEnvelope<object>>(_jsonOptions, cancellationToken);
            return (response.IsSuccessStatusCode, envelope?.Message ?? (response.IsSuccessStatusCode ? "Password changed." : "Password change failed."));
        }
        catch (Exception ex)
        {
            return (false, $"Network error: {ex.Message}");
        }
    }

    public async Task<(bool Success, string Message)> ForgotPasswordAsync(ForgotPasswordRequestDto request, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/auth/forgot-password", request, _jsonOptions, cancellationToken);
            var envelope = await response.Content.ReadFromJsonAsync<ApiResponseEnvelope<object>>(_jsonOptions, cancellationToken);
            return (response.IsSuccessStatusCode, envelope?.Message ?? "Request processed.");
        }
        catch (Exception ex)
        {
            return (false, $"Network error: {ex.Message}");
        }
    }

    public async Task<(bool Success, string Message)> ResetPasswordAsync(ResetPasswordRequestDto request, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/auth/reset-password", request, _jsonOptions, cancellationToken);
            var envelope = await response.Content.ReadFromJsonAsync<ApiResponseEnvelope<object>>(_jsonOptions, cancellationToken);
            return (response.IsSuccessStatusCode, envelope?.Message ?? (response.IsSuccessStatusCode ? "Password reset successfully." : "Password reset failed."));
        }
        catch (Exception ex)
        {
            return (false, $"Network error: {ex.Message}");
        }
    }

    public async Task<UserDto?> GetProfileAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("api/auth/profile", cancellationToken);
            if (!response.IsSuccessStatusCode)
                return null;

            var envelope = await response.Content.ReadFromJsonAsync<ApiResponseEnvelope<UserDto>>(_jsonOptions, cancellationToken);
            return envelope?.Data;
        }
        catch
        {
            return null;
        }
    }

    public async Task<(bool Success, string Message)> UpdateProfileAsync(UpdateProfileRequestDto request, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync("api/auth/profile", request, _jsonOptions, cancellationToken);
            var envelope = await response.Content.ReadFromJsonAsync<ApiResponseEnvelope<object>>(_jsonOptions, cancellationToken);
            return (response.IsSuccessStatusCode, envelope?.Message ?? (response.IsSuccessStatusCode ? "Profile updated." : "Profile update failed."));
        }
        catch (Exception ex)
        {
            return (false, $"Network error: {ex.Message}");
        }
    }

    public async Task<IReadOnlyList<OrganizationSummaryDto>> GetOrganizationsAsync(string? search = null, bool? isActive = null, CancellationToken cancellationToken = default)
    {
        try
        {
            string url = "api/organizations";
            var queryParams = new List<string>();
            if (!string.IsNullOrWhiteSpace(search)) queryParams.Add($"search={Uri.EscapeDataString(search)}");
            if (isActive.HasValue) queryParams.Add($"isActive={isActive.Value}");
            if (queryParams.Count > 0) url += "?" + string.Join("&", queryParams);

            var response = await _httpClient.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode) return Array.Empty<OrganizationSummaryDto>();

            var envelope = await response.Content.ReadFromJsonAsync<ApiResponseEnvelope<List<OrganizationSummaryDto>>>(_jsonOptions, cancellationToken);
            return envelope?.Data ?? (IReadOnlyList<OrganizationSummaryDto>)Array.Empty<OrganizationSummaryDto>();
        }
        catch
        {
            return Array.Empty<OrganizationSummaryDto>();
        }
    }

    public async Task<OrganizationDetailsDto?> GetOrganizationByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"api/organizations/{id}", cancellationToken);
            if (!response.IsSuccessStatusCode) return null;

            var envelope = await response.Content.ReadFromJsonAsync<ApiResponseEnvelope<OrganizationDetailsDto>>(_jsonOptions, cancellationToken);
            return envelope?.Data;
        }
        catch
        {
            return null;
        }
    }

    public async Task<(bool Success, string Message)> CreateOrganizationAsync(CreateOrganizationRequestDto request, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/organizations", request, _jsonOptions, cancellationToken);
            var envelope = await response.Content.ReadFromJsonAsync<ApiResponseEnvelope<object>>(_jsonOptions, cancellationToken);
            return (response.IsSuccessStatusCode, envelope?.Message ?? (response.IsSuccessStatusCode ? "Organization created." : "Creation failed."));
        }
        catch (Exception ex)
        {
            return (false, $"Network error: {ex.Message}");
        }
    }

    public async Task<(bool Success, string Message)> UpdateOrganizationAsync(Guid id, UpdateOrganizationRequestDto request, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"api/organizations/{id}", request, _jsonOptions, cancellationToken);
            var envelope = await response.Content.ReadFromJsonAsync<ApiResponseEnvelope<object>>(_jsonOptions, cancellationToken);
            return (response.IsSuccessStatusCode, envelope?.Message ?? (response.IsSuccessStatusCode ? "Organization updated." : "Update failed."));
        }
        catch (Exception ex)
        {
            return (false, $"Network error: {ex.Message}");
        }
    }

    public async Task<(bool Success, string Message)> ToggleOrganizationStatusAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PatchAsync($"api/organizations/{id}/toggle-status", null, cancellationToken);
            var envelope = await response.Content.ReadFromJsonAsync<ApiResponseEnvelope<object>>(_jsonOptions, cancellationToken);
            return (response.IsSuccessStatusCode, envelope?.Message ?? "Status updated.");
        }
        catch (Exception ex)
        {
            return (false, $"Network error: {ex.Message}");
        }
    }

    public async Task<IReadOnlyList<BranchDto>> GetBranchesAsync(Guid? organizationId = null, bool? isActive = null, CancellationToken cancellationToken = default)
    {
        try
        {
            string url = "api/branches";
            var queryParams = new List<string>();
            if (organizationId.HasValue) queryParams.Add($"organizationId={organizationId.Value}");
            if (isActive.HasValue) queryParams.Add($"isActive={isActive.Value}");
            if (queryParams.Count > 0) url += "?" + string.Join("&", queryParams);

            var response = await _httpClient.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode) return Array.Empty<BranchDto>();

            var envelope = await response.Content.ReadFromJsonAsync<ApiResponseEnvelope<List<BranchDto>>>(_jsonOptions, cancellationToken);
            return envelope?.Data ?? (IReadOnlyList<BranchDto>)Array.Empty<BranchDto>();
        }
        catch
        {
            return Array.Empty<BranchDto>();
        }
    }

    public async Task<(bool Success, string Message)> CreateBranchAsync(CreateBranchRequestDto request, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/branches", request, _jsonOptions, cancellationToken);
            var envelope = await response.Content.ReadFromJsonAsync<ApiResponseEnvelope<object>>(_jsonOptions, cancellationToken);
            return (response.IsSuccessStatusCode, envelope?.Message ?? (response.IsSuccessStatusCode ? "Branch created." : "Creation failed."));
        }
        catch (Exception ex)
        {
            return (false, $"Network error: {ex.Message}");
        }
    }

    public async Task<(bool Success, string Message)> UpdateBranchAsync(Guid id, UpdateBranchRequestDto request, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"api/branches/{id}", request, _jsonOptions, cancellationToken);
            var envelope = await response.Content.ReadFromJsonAsync<ApiResponseEnvelope<object>>(_jsonOptions, cancellationToken);
            return (response.IsSuccessStatusCode, envelope?.Message ?? (response.IsSuccessStatusCode ? "Branch updated." : "Update failed."));
        }
        catch (Exception ex)
        {
            return (false, $"Network error: {ex.Message}");
        }
    }

    public async Task<(bool Success, string Message)> DeleteBranchAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"api/branches/{id}", cancellationToken);
            var envelope = await response.Content.ReadFromJsonAsync<ApiResponseEnvelope<object>>(_jsonOptions, cancellationToken);
            return (response.IsSuccessStatusCode, envelope?.Message ?? (response.IsSuccessStatusCode ? "Branch deleted." : "Deletion failed."));
        }
        catch (Exception ex)
        {
            return (false, $"Network error: {ex.Message}");
        }
    }

    public async Task<AuthResultDto> SwitchBranchAsync(Guid branchId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsync($"api/auth/switch-branch/{branchId}", null, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                return AuthResultDto.Failed($"Switch branch failed ({response.StatusCode}): {errorContent}");
            }

            var envelope = await response.Content.ReadFromJsonAsync<ApiResponseEnvelope<AuthResultDto>>(_jsonOptions, cancellationToken);
            return envelope?.Data ?? AuthResultDto.Failed("Empty response from server.");
        }
        catch (Exception ex)
        {
            return AuthResultDto.Failed($"Network error: {ex.Message}");
        }
    }

    public async Task<SchoolProfileDto?> GetMySchoolAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("api/organizations/my-school", cancellationToken);
            if (!response.IsSuccessStatusCode) return null;

            var envelope = await response.Content.ReadFromJsonAsync<ApiResponseEnvelope<SchoolProfileDto>>(_jsonOptions, cancellationToken);
            return envelope?.Data;
        }
        catch
        {
            return null;
        }
    }

    public async Task<(bool Success, string Message)> UpdateMySchoolAsync(UpdateSchoolProfileRequestDto request, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync("api/organizations/my-school", request, _jsonOptions, cancellationToken);
            var envelope = await response.Content.ReadFromJsonAsync<ApiResponseEnvelope<object>>(_jsonOptions, cancellationToken);
            return (response.IsSuccessStatusCode, envelope?.Message ?? (response.IsSuccessStatusCode ? "School updated." : "Update failed."));
        }
        catch (Exception ex)
        {
            return (false, $"Network error: {ex.Message}");
        }
    }

    public async Task<DashboardSummaryDto?> GetDashboardSummaryAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("api/dashboard/summary", cancellationToken);
            if (!response.IsSuccessStatusCode) return null;

            var envelope = await response.Content.ReadFromJsonAsync<ApiResponseEnvelope<DashboardSummaryDto>>(_jsonOptions, cancellationToken);
            return envelope?.Data;
        }
        catch
        {
            return null;
        }
    }

    // ==========================================
    // Phase 7: Student Management
    // ==========================================
    public async Task<IReadOnlyList<StudentSummaryDto>> GetStudentsAsync(
        string? search = null,
        string? grade = null,
        string? section = null,
        StudentStatus? status = null,
        Guid? branchId = null,
        int pageNumber = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = new List<string>
            {
                $"pageNumber={pageNumber}",
                $"pageSize={pageSize}"
            };
            if (!string.IsNullOrWhiteSpace(search)) query.Add($"search={Uri.EscapeDataString(search.Trim())}");
            if (!string.IsNullOrWhiteSpace(grade)) query.Add($"grade={Uri.EscapeDataString(grade.Trim())}");
            if (!string.IsNullOrWhiteSpace(section)) query.Add($"section={Uri.EscapeDataString(section.Trim())}");
            if (status.HasValue) query.Add($"status={status.Value}");
            if (branchId.HasValue) query.Add($"branchId={branchId.Value}");

            string url = $"api/students?{string.Join("&", query)}";
            var response = await _httpClient.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode) return Array.Empty<StudentSummaryDto>();

            var envelope = await response.Content.ReadFromJsonAsync<ApiResponseEnvelope<PaginatedListResponse<StudentSummaryDto>>>(_jsonOptions, cancellationToken);
            return envelope?.Data?.Items ?? (IReadOnlyList<StudentSummaryDto>)Array.Empty<StudentSummaryDto>();
        }
        catch
        {
            return Array.Empty<StudentSummaryDto>();
        }
    }

    public async Task<StudentDetailsDto?> GetStudentByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"api/students/{id}", cancellationToken);
            if (!response.IsSuccessStatusCode) return null;

            var envelope = await response.Content.ReadFromJsonAsync<ApiResponseEnvelope<StudentDetailsDto>>(_jsonOptions, cancellationToken);
            return envelope?.Data;
        }
        catch
        {
            return null;
        }
    }

    public async Task<(bool Success, string Message, Guid? StudentId)> CreateStudentAsync(CreateStudentRequestDto request, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/students", request, _jsonOptions, cancellationToken);
            var envelope = await response.Content.ReadFromJsonAsync<ApiResponseEnvelope<EntityCreationResult>>(_jsonOptions, cancellationToken);
            return (response.IsSuccessStatusCode, envelope?.Message ?? (response.IsSuccessStatusCode ? "Student admitted." : "Admission failed."), envelope?.Data?.Id);
        }
        catch (Exception ex)
        {
            return (false, $"Network error: {ex.Message}", null);
        }
    }

    public async Task<(bool Success, string Message)> UpdateStudentAsync(Guid id, UpdateStudentRequestDto request, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"api/students/{id}", request, _jsonOptions, cancellationToken);
            var envelope = await response.Content.ReadFromJsonAsync<ApiResponseEnvelope<object>>(_jsonOptions, cancellationToken);
            return (response.IsSuccessStatusCode, envelope?.Message ?? (response.IsSuccessStatusCode ? "Student updated." : "Update failed."));
        }
        catch (Exception ex)
        {
            return (false, $"Network error: {ex.Message}");
        }
    }

    public async Task<(bool Success, string Message)> DeleteStudentAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"api/students/{id}", cancellationToken);
            var envelope = await response.Content.ReadFromJsonAsync<ApiResponseEnvelope<object>>(_jsonOptions, cancellationToken);
            return (response.IsSuccessStatusCode, envelope?.Message ?? (response.IsSuccessStatusCode ? "Student deleted." : "Deletion failed."));
        }
        catch (Exception ex)
        {
            return (false, $"Network error: {ex.Message}");
        }
    }

    public async Task<(bool Success, string Message)> AssignGuardianAsync(Guid studentId, AssignGuardianRequestDto request, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"api/students/{studentId}/guardians", request, _jsonOptions, cancellationToken);
            var envelope = await response.Content.ReadFromJsonAsync<ApiResponseEnvelope<object>>(_jsonOptions, cancellationToken);
            return (response.IsSuccessStatusCode, envelope?.Message ?? (response.IsSuccessStatusCode ? "Guardian assigned." : "Assignment failed."));
        }
        catch (Exception ex)
        {
            return (false, $"Network error: {ex.Message}");
        }
    }

    public async Task<(bool Success, string Message)> UnlinkGuardianAsync(Guid studentId, Guid guardianId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"api/students/{studentId}/guardians/{guardianId}", cancellationToken);
            var envelope = await response.Content.ReadFromJsonAsync<ApiResponseEnvelope<object>>(_jsonOptions, cancellationToken);
            return (response.IsSuccessStatusCode, envelope?.Message ?? (response.IsSuccessStatusCode ? "Guardian unlinked." : "Unlinking failed."));
        }
        catch (Exception ex)
        {
            return (false, $"Network error: {ex.Message}");
        }
    }

    // ==========================================
    // Phase 7: Parent & Guardian Management
    // ==========================================
    public async Task<IReadOnlyList<GuardianSummaryDto>> GetGuardiansAsync(
        string? search = null,
        Guid? branchId = null,
        int pageNumber = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = new List<string>
            {
                $"pageNumber={pageNumber}",
                $"pageSize={pageSize}"
            };
            if (!string.IsNullOrWhiteSpace(search)) query.Add($"search={Uri.EscapeDataString(search.Trim())}");
            if (branchId.HasValue) query.Add($"branchId={branchId.Value}");

            string url = $"api/guardians?{string.Join("&", query)}";
            var response = await _httpClient.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode) return Array.Empty<GuardianSummaryDto>();

            var envelope = await response.Content.ReadFromJsonAsync<ApiResponseEnvelope<PaginatedListResponse<GuardianSummaryDto>>>(_jsonOptions, cancellationToken);
            return envelope?.Data?.Items ?? (IReadOnlyList<GuardianSummaryDto>)Array.Empty<GuardianSummaryDto>();
        }
        catch
        {
            return Array.Empty<GuardianSummaryDto>();
        }
    }

    public async Task<GuardianDetailsDto?> GetGuardianByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"api/guardians/{id}", cancellationToken);
            if (!response.IsSuccessStatusCode) return null;

            var envelope = await response.Content.ReadFromJsonAsync<ApiResponseEnvelope<GuardianDetailsDto>>(_jsonOptions, cancellationToken);
            return envelope?.Data;
        }
        catch
        {
            return null;
        }
    }

    public async Task<(bool Success, string Message, Guid? GuardianId)> CreateGuardianAsync(CreateGuardianRequestDto request, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/guardians", request, _jsonOptions, cancellationToken);
            var envelope = await response.Content.ReadFromJsonAsync<ApiResponseEnvelope<EntityCreationResult>>(_jsonOptions, cancellationToken);
            return (response.IsSuccessStatusCode, envelope?.Message ?? (response.IsSuccessStatusCode ? "Guardian created." : "Creation failed."), envelope?.Data?.Id);
        }
        catch (Exception ex)
        {
            return (false, $"Network error: {ex.Message}", null);
        }
    }

    public async Task<(bool Success, string Message)> UpdateGuardianAsync(Guid id, UpdateGuardianRequestDto request, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"api/guardians/{id}", request, _jsonOptions, cancellationToken);
            var envelope = await response.Content.ReadFromJsonAsync<ApiResponseEnvelope<object>>(_jsonOptions, cancellationToken);
            return (response.IsSuccessStatusCode, envelope?.Message ?? (response.IsSuccessStatusCode ? "Guardian updated." : "Update failed."));
        }
        catch (Exception ex)
        {
            return (false, $"Network error: {ex.Message}");
        }
    }

    public async Task<(bool Success, string Message)> DeleteGuardianAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"api/guardians/{id}", cancellationToken);
            var envelope = await response.Content.ReadFromJsonAsync<ApiResponseEnvelope<object>>(_jsonOptions, cancellationToken);
            return (response.IsSuccessStatusCode, envelope?.Message ?? (response.IsSuccessStatusCode ? "Guardian deleted." : "Deletion failed."));
        }
        catch (Exception ex)
        {
            return (false, $"Network error: {ex.Message}");
        }
    }

    // ==========================================
    // Phase 7: Staff & Teacher Management
    // ==========================================
    public async Task<IReadOnlyList<StaffSummaryDto>> GetStaffAsync(
        string? search = null,
        string? department = null,
        string? designation = null,
        EmploymentType? employmentType = null,
        StaffStatus? status = null,
        Guid? branchId = null,
        int pageNumber = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = new List<string>
            {
                $"pageNumber={pageNumber}",
                $"pageSize={pageSize}"
            };
            if (!string.IsNullOrWhiteSpace(search)) query.Add($"search={Uri.EscapeDataString(search.Trim())}");
            if (!string.IsNullOrWhiteSpace(department)) query.Add($"department={Uri.EscapeDataString(department.Trim())}");
            if (!string.IsNullOrWhiteSpace(designation)) query.Add($"designation={Uri.EscapeDataString(designation.Trim())}");
            if (employmentType.HasValue) query.Add($"employmentType={employmentType.Value}");
            if (status.HasValue) query.Add($"status={status.Value}");
            if (branchId.HasValue) query.Add($"branchId={branchId.Value}");

            string url = $"api/staff?{string.Join("&", query)}";
            var response = await _httpClient.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode) return Array.Empty<StaffSummaryDto>();

            var envelope = await response.Content.ReadFromJsonAsync<ApiResponseEnvelope<PaginatedListResponse<StaffSummaryDto>>>(_jsonOptions, cancellationToken);
            return envelope?.Data?.Items ?? (IReadOnlyList<StaffSummaryDto>)Array.Empty<StaffSummaryDto>();
        }
        catch
        {
            return Array.Empty<StaffSummaryDto>();
        }
    }

    public async Task<StaffDetailsDto?> GetStaffByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"api/staff/{id}", cancellationToken);
            if (!response.IsSuccessStatusCode) return null;

            var envelope = await response.Content.ReadFromJsonAsync<ApiResponseEnvelope<StaffDetailsDto>>(_jsonOptions, cancellationToken);
            return envelope?.Data;
        }
        catch
        {
            return null;
        }
    }

    public async Task<(bool Success, string Message, Guid? StaffId)> CreateStaffAsync(CreateStaffRequestDto request, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/staff", request, _jsonOptions, cancellationToken);
            var envelope = await response.Content.ReadFromJsonAsync<ApiResponseEnvelope<EntityCreationResult>>(_jsonOptions, cancellationToken);
            return (response.IsSuccessStatusCode, envelope?.Message ?? (response.IsSuccessStatusCode ? "Staff onboarded." : "Onboarding failed."), envelope?.Data?.Id);
        }
        catch (Exception ex)
        {
            return (false, $"Network error: {ex.Message}", null);
        }
    }

    public async Task<(bool Success, string Message)> UpdateStaffAsync(Guid id, UpdateStaffRequestDto request, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"api/staff/{id}", request, _jsonOptions, cancellationToken);
            var envelope = await response.Content.ReadFromJsonAsync<ApiResponseEnvelope<object>>(_jsonOptions, cancellationToken);
            return (response.IsSuccessStatusCode, envelope?.Message ?? (response.IsSuccessStatusCode ? "Staff updated." : "Update failed."));
        }
        catch (Exception ex)
        {
            return (false, $"Network error: {ex.Message}");
        }
    }

    public async Task<(bool Success, string Message)> ToggleStaffStatusAsync(Guid id, StaffStatus newStatus, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PatchAsync($"api/staff/{id}/toggle-status?newStatus={newStatus}", null, cancellationToken);
            var envelope = await response.Content.ReadFromJsonAsync<ApiResponseEnvelope<object>>(_jsonOptions, cancellationToken);
            return (response.IsSuccessStatusCode, envelope?.Message ?? (response.IsSuccessStatusCode ? "Status changed." : "Status change failed."));
        }
        catch (Exception ex)
        {
            return (false, $"Network error: {ex.Message}");
        }
    }

    public async Task<(bool Success, string Message)> DeleteStaffAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"api/staff/{id}", cancellationToken);
            var envelope = await response.Content.ReadFromJsonAsync<ApiResponseEnvelope<object>>(_jsonOptions, cancellationToken);
            return (response.IsSuccessStatusCode, envelope?.Message ?? (response.IsSuccessStatusCode ? "Staff deleted." : "Deletion failed."));
        }
        catch (Exception ex)
        {
            return (false, $"Network error: {ex.Message}");
        }
    }

    private class ApiResponseEnvelope<T>
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public T? Data { get; set; }
    }

    private class PaginatedListResponse<T>
    {
        public List<T> Items { get; set; } = new();
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages { get; set; }
    }

    private class EntityCreationResult
    {
        public Guid Id { get; set; }
    }
}
