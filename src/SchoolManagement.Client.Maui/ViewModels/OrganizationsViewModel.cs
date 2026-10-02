using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolManagement.Application.Common.Models;
using SchoolManagement.Client.Core.Auth;
using SchoolManagement.Client.Core.Services;

namespace SchoolManagement.Client.Maui.ViewModels;

public partial class OrganizationsViewModel : BaseViewModel
{
    private readonly IApiClient _apiClient;
    private readonly ClientAuthState _authState;

    public OrganizationsViewModel(IApiClient apiClient, ClientAuthState authState)
    {
        _apiClient = apiClient;
        _authState = authState;
        Title = "Organizations & Schools";
    }

    public ObservableCollection<OrganizationSummaryDto> Organizations { get; } = new();

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial OrganizationSummaryDto? SelectedOrganization { get; set; }

    [ObservableProperty]
    public partial bool IsCreateModalVisible { get; set; } = false;

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = string.Empty;

    // Create Organization Form Fields
    [ObservableProperty]
    public partial string NewOrgName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string NewOrgCode { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string NewOrgEmail { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string NewOrgPhone { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string NewOrgAddress { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string NewAdminFirstName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string NewAdminLastName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string NewAdminEmail { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string NewAdminPassword { get; set; } = "SchoolAdmin@2026!";

    [ObservableProperty]
    public partial string NewThemeColor { get; set; } = "#6C5CE7";

    [RelayCommand]
    public async Task LoadOrganizationsAsync()
    {
        if (IsBusy) return;

        try
        {
            IsBusy = true;
            StatusMessage = "Loading schools...";

            var list = await _apiClient.GetOrganizationsAsync(SearchText);
            Organizations.Clear();
            foreach (var org in list)
            {
                Organizations.Add(org);
            }

            StatusMessage = $"{Organizations.Count} organization(s) loaded.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading organizations: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public void OpenCreateModal()
    {
        NewOrgName = string.Empty;
        NewOrgCode = string.Empty;
        NewOrgEmail = string.Empty;
        NewOrgPhone = string.Empty;
        NewOrgAddress = string.Empty;
        NewAdminFirstName = string.Empty;
        NewAdminLastName = string.Empty;
        NewAdminEmail = string.Empty;
        NewAdminPassword = "SchoolAdmin@2026!";
        NewThemeColor = "#6C5CE7";
        IsCreateModalVisible = true;
    }

    [RelayCommand]
    public void CloseCreateModal()
    {
        IsCreateModalVisible = false;
    }

    [RelayCommand]
    public async Task CreateOrganizationAsync()
    {
        if (string.IsNullOrWhiteSpace(NewOrgName) || string.IsNullOrWhiteSpace(NewOrgCode))
        {
            StatusMessage = "Organization Name and Code are mandatory.";
            return;
        }

        if (string.IsNullOrWhiteSpace(NewAdminEmail) || string.IsNullOrWhiteSpace(NewAdminPassword))
        {
            StatusMessage = "Administrator Email and Password are required.";
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = "Creating organization...";

            var request = new CreateOrganizationRequestDto(
                NewOrgName,
                NewOrgCode,
                NewOrgEmail,
                NewOrgPhone,
                NewOrgAddress,
                null,
                NewThemeColor,
                "#5840BA",
                NewAdminEmail,
                NewAdminEmail,
                NewAdminPassword,
                NewAdminFirstName,
                NewAdminLastName
            );

            var (success, message) = await _apiClient.CreateOrganizationAsync(request);
            if (success)
            {
                IsCreateModalVisible = false;
                StatusMessage = "School created successfully!";
                await LoadOrganizationsAsync();
            }
            else
            {
                StatusMessage = $"Failed: {message}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task ToggleStatusAsync(OrganizationSummaryDto? org)
    {
        if (org == null) return;

        try
        {
            IsBusy = true;
            var (success, message) = await _apiClient.ToggleOrganizationStatusAsync(org.Id);
            StatusMessage = success ? message : $"Failed: {message}";
            await LoadOrganizationsAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
