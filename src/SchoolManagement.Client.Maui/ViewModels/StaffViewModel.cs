using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolManagement.Application.Common.Models;
using SchoolManagement.Client.Core.Auth;
using SchoolManagement.Client.Core.Services;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Client.Maui.ViewModels;

public partial class StaffViewModel : BaseViewModel
{
    private readonly IApiClient _apiClient;
    private readonly ClientAuthState _authState;

    public StaffViewModel(IApiClient apiClient, ClientAuthState authState)
    {
        _apiClient = apiClient;
        _authState = authState;
        Title = "Staff & Teachers Directory";
    }

    public ObservableCollection<StaffSummaryDto> StaffList { get; } = new();

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? SelectedDepartment { get; set; }

    [ObservableProperty]
    public partial StaffSummaryDto? SelectedStaff { get; set; }

    [ObservableProperty]
    public partial StaffDetailsDto? CurrentStaffDetails { get; set; }

    [ObservableProperty]
    public partial bool IsOnboardModalVisible { get; set; } = false;

    [ObservableProperty]
    public partial bool IsDetailsModalVisible { get; set; } = false;

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial int TotalStaffCount { get; set; }

    // New Staff Form Fields
    [ObservableProperty]
    public partial string NewFirstName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string NewLastName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string NewEmail { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string NewPhoneNumber { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string NewDesignation { get; set; } = "Teacher";

    [ObservableProperty]
    public partial string NewDepartment { get; set; } = "Academics";

    [ObservableProperty]
    public partial string NewQualification { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string NewSpecialization { get; set; } = string.Empty;

    [ObservableProperty]
    public partial EmploymentType NewEmploymentType { get; set; } = EmploymentType.FullTime;

    [ObservableProperty]
    public partial decimal? NewBasicSalary { get; set; } = 60000;

    [ObservableProperty]
    public partial string NewNationalId { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string NewPassword { get; set; } = "Staff@2026!Pak";

    [RelayCommand]
    public async Task LoadStaffAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        ClearError();
        StatusMessage = string.Empty;

        try
        {
            var items = await _apiClient.GetStaffAsync(
                search: string.IsNullOrWhiteSpace(SearchText) ? null : SearchText,
                department: string.IsNullOrWhiteSpace(SelectedDepartment) ? null : SelectedDepartment,
                pageSize: 100);

            StaffList.Clear();
            foreach (var staff in items)
            {
                StaffList.Add(staff);
            }
            TotalStaffCount = StaffList.Count;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load staff list: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task SearchStaffAsync()
    {
        await LoadStaffAsync();
    }

    [RelayCommand]
    public void OpenOnboardModal()
    {
        ClearError();
        StatusMessage = string.Empty;
        NewFirstName = string.Empty;
        NewLastName = string.Empty;
        NewEmail = string.Empty;
        NewPhoneNumber = string.Empty;
        NewQualification = string.Empty;
        NewSpecialization = string.Empty;
        NewNationalId = string.Empty;
        IsOnboardModalVisible = true;
    }

    [RelayCommand]
    public void CloseOnboardModal()
    {
        IsOnboardModalVisible = false;
    }

    [RelayCommand]
    public async Task SubmitOnboardAsync()
    {
        if (string.IsNullOrWhiteSpace(NewFirstName) || string.IsNullOrWhiteSpace(NewLastName))
        {
            ErrorMessage = "First name and last name are required.";
            return;
        }

        if (string.IsNullOrWhiteSpace(NewEmail))
        {
            ErrorMessage = "Email address is required.";
            return;
        }

        if (string.IsNullOrWhiteSpace(NewDesignation))
        {
            ErrorMessage = "Designation is required.";
            return;
        }

        IsBusy = true;
        ClearError();

        try
        {
            var request = new CreateStaffRequestDto(
                BranchId: _authState.CurrentUser?.BranchId,
                EmployeeCode: null, // Auto-generate
                FirstName: NewFirstName.Trim(),
                LastName: NewLastName.Trim(),
                Email: NewEmail.Trim(),
                PhoneNumber: string.IsNullOrWhiteSpace(NewPhoneNumber) ? null : NewPhoneNumber.Trim(),
                Designation: NewDesignation.Trim(),
                Department: NewDepartment.Trim(),
                Qualification: string.IsNullOrWhiteSpace(NewQualification) ? null : NewQualification.Trim(),
                Specialization: string.IsNullOrWhiteSpace(NewSpecialization) ? null : NewSpecialization.Trim(),
                EmploymentType: NewEmploymentType,
                JoiningDate: DateTime.UtcNow,
                BasicSalary: NewBasicSalary,
                EmergencyContactName: null,
                EmergencyContactPhone: null,
                NationalId: string.IsNullOrWhiteSpace(NewNationalId) ? null : NewNationalId.Trim(),
                InitialPassword: string.IsNullOrWhiteSpace(NewPassword) ? "Staff@2026!Pak" : NewPassword.Trim(),
                RoleIds: null
            );

            var (success, message, staffId) = await _apiClient.CreateStaffAsync(request);
            if (success)
            {
                StatusMessage = "Staff member onboarded successfully!";
                IsOnboardModalVisible = false;
                await LoadStaffAsync();
            }
            else
            {
                ErrorMessage = message;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error onboarding staff: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task ViewStaffDetailsAsync(StaffSummaryDto staff)
    {
        if (staff == null) return;
        IsBusy = true;
        ClearError();

        try
        {
            var details = await _apiClient.GetStaffByIdAsync(staff.Id);
            if (details != null)
            {
                CurrentStaffDetails = details;
                IsDetailsModalVisible = true;
            }
            else
            {
                ErrorMessage = "Could not load staff profile.";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error loading profile: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public void CloseDetailsModal()
    {
        IsDetailsModalVisible = false;
        CurrentStaffDetails = null;
    }

    [RelayCommand]
    public async Task ToggleStatusAsync(StaffSummaryDto staff)
    {
        if (staff == null) return;
        IsBusy = true;
        ClearError();

        try
        {
            var newStatus = staff.Status == StaffStatus.Active ? StaffStatus.OnLeave : StaffStatus.Active;
            var (success, message) = await _apiClient.ToggleStaffStatusAsync(staff.Id, newStatus);
            if (success)
            {
                StatusMessage = $"Updated status to {newStatus}.";
                await LoadStaffAsync();
            }
            else
            {
                ErrorMessage = message;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error toggling status: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task DeleteStaffAsync(StaffSummaryDto staff)
    {
        if (staff == null) return;
        IsBusy = true;
        ClearError();

        try
        {
            var (success, message) = await _apiClient.DeleteStaffAsync(staff.Id);
            if (success)
            {
                StatusMessage = $"Staff member '{staff.FullName}' record deleted.";
                await LoadStaffAsync();
            }
            else
            {
                ErrorMessage = message;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error deleting staff: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task GoBackAsync()
    {
        await Shell.Current.GoToAsync("//DashboardPage");
    }
}
