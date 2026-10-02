using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolManagement.Application.Common.Models;
using SchoolManagement.Client.Core.Auth;
using SchoolManagement.Client.Core.Services;
using SchoolManagement.Client.Core.Sync;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Client.Maui.ViewModels;

public partial class DashboardViewModel : BaseViewModel
{
    private readonly ClientAuthState _authState;
    private readonly ISyncEngine _syncEngine;
    private readonly IApiClient _apiClient;

    public DashboardViewModel(
        ClientAuthState authState,
        ISyncEngine syncEngine,
        IApiClient apiClient)
    {
        _authState = authState;
        _syncEngine = syncEngine;
        _apiClient = apiClient;
        Title = "School Management Dashboard";

        LoadUserData();
        _ = LoadDashboardSummaryAsync();
    }

    [ObservableProperty]
    public partial string UserName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string UserEmail { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string UserRole { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string OrganizationName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string OrganizationCode { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ActiveBranchName { get; set; } = "Main Campus";

    [ObservableProperty]
    public partial int TotalBranches { get; set; }

    [ObservableProperty]
    public partial int TotalStaff { get; set; }

    [ObservableProperty]
    public partial int TotalStudents { get; set; }

    [ObservableProperty]
    public partial int TotalUsers { get; set; }

    [ObservableProperty]
    public partial bool IsSuperAdmin { get; set; }

    [ObservableProperty]
    public partial bool IsOrgAdmin { get; set; }

    [ObservableProperty]
    public partial bool ShowBranchSwitcher { get; set; }

    [ObservableProperty]
    public partial int PendingSyncCount { get; set; }

    [ObservableProperty]
    public partial string SyncStatusMessage { get; set; } = "In Sync";

    public ObservableCollection<BranchDto> AvailableBranches { get; } = new();

    public ObservableCollection<RecentActivityDto> RecentActivities { get; } = new();

    public void LoadUserData()
    {
        var user = _authState.CurrentUser;
        if (user != null)
        {
            UserName = user.FullName;
            UserEmail = user.Email;
            UserRole = user.RoleType.ToString();
            OrganizationName = user.OrganizationName ?? "Master Platform";
            ActiveBranchName = user.BranchName ?? "Main Campus";
            IsSuperAdmin = user.RoleType == UserRoleType.SuperAdmin;
            IsOrgAdmin = user.RoleType == UserRoleType.OrganizationAdmin;
        }
    }

    [RelayCommand]
    public async Task LoadDashboardSummaryAsync()
    {
        try
        {
            var summary = await _apiClient.GetDashboardSummaryAsync();
            if (summary != null)
            {
                TotalBranches = summary.TotalBranches;
                TotalStaff = summary.TotalStaff;
                TotalStudents = summary.TotalStudents;
                TotalUsers = summary.TotalUsers;

                if (!string.IsNullOrWhiteSpace(summary.BranchName))
                    ActiveBranchName = summary.BranchName;

                RecentActivities.Clear();
                foreach (var act in summary.RecentActivities)
                {
                    RecentActivities.Add(act);
                }
            }

            // Load available branches for switching if tenant user
            if (_authState.CurrentUser != null && _authState.CurrentUser.OrganizationId != Guid.Empty)
            {
                var branches = await _apiClient.GetBranchesAsync(_authState.CurrentUser.OrganizationId, isActive: true);
                AvailableBranches.Clear();
                foreach (var b in branches)
                {
                    AvailableBranches.Add(b);
                }
            }
        }
        catch
        {
            // Fallback gracefully in offline mode
        }
    }

    [RelayCommand]
    public void ToggleBranchSwitcher()
    {
        ShowBranchSwitcher = !ShowBranchSwitcher;
    }

    [RelayCommand]
    public async Task SwitchBranchAsync(BranchDto branch)
    {
        if (branch == null || IsBusy) return;

        try
        {
            IsBusy = true;
            ShowBranchSwitcher = false;

            var result = await _apiClient.SwitchBranchAsync(branch.Id);
            if (result.Success && result.User != null && !string.IsNullOrWhiteSpace(result.AccessToken))
            {
                _authState.SetAuthenticated(
                    result.User,
                    result.AccessToken,
                    result.RefreshToken ?? string.Empty,
                    result.ExpiresAtUtc ?? DateTime.UtcNow.AddHours(1));

                _apiClient.SetAuthToken(result.AccessToken);
                ActiveBranchName = branch.Name;
                LoadUserData();

                await LoadDashboardSummaryAsync();
            }
        }
        catch
        {
            // Error handling
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task NavigateToSchoolProfileAsync()
    {
        await Shell.Current.GoToAsync("SchoolProfilePage");
    }

    [RelayCommand]
    public async Task NavigateToOrganizationsAsync()
    {
        await Shell.Current.GoToAsync("OrganizationsPage");
    }

    [RelayCommand]
    public async Task TriggerSyncAsync()
    {
        if (IsBusy || _authState.CurrentUser == null)
            return;

        try
        {
            IsBusy = true;
            SyncStatusMessage = "Synchronizing...";

            bool success = await _syncEngine.SyncNowAsync(_authState.CurrentUser.OrganizationId);
            if (success)
            {
                PendingSyncCount = await _syncEngine.GetPendingQueueCountAsync(_authState.CurrentUser.OrganizationId);
                SyncStatusMessage = PendingSyncCount == 0 ? "All changes synced" : $"{PendingSyncCount} pending";
            }
            else
            {
                SyncStatusMessage = "Sync paused (Offline / Server unavailable)";
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task NavigateToStudentsAsync()
    {
        await Shell.Current.GoToAsync("//StudentsPage");
    }

    [RelayCommand]
    public async Task NavigateToStaffAsync()
    {
        await Shell.Current.GoToAsync("//StaffPage");
    }

    [RelayCommand]
    public async Task LogoutAsync()
    {
        _authState.SetLoggedOut();
        _apiClient.SetAuthToken(null);
        await Shell.Current.GoToAsync("//LoginPage");
    }
}
