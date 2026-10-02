using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolManagement.Application.Common.Models;
using SchoolManagement.Client.Core.Auth;
using SchoolManagement.Client.Core.Services;

namespace SchoolManagement.Client.Maui.ViewModels;

public partial class SchoolProfileViewModel : BaseViewModel
{
    private readonly IApiClient _apiClient;
    private readonly ClientAuthState _authState;

    public SchoolProfileViewModel(IApiClient apiClient, ClientAuthState authState)
    {
        _apiClient = apiClient;
        _authState = authState;
        Title = "School Profile & Branding";

        _ = LoadProfileAsync();
    }

    [ObservableProperty]
    public partial string SchoolName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SchoolCode { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Email { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Phone { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Address { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string LogoUrl { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ThemePrimaryColor { get; set; } = "#6C5CE7";

    [ObservableProperty]
    public partial string ThemeSecondaryColor { get; set; } = "#5840BA";

    [ObservableProperty]
    public partial bool ThemeDarkMode { get; set; } = true;

    [ObservableProperty]
    public partial int BranchesCount { get; set; }

    [ObservableProperty]
    public partial int UsersCount { get; set; }

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasStatusMessage { get; set; }

    [ObservableProperty]
    public partial bool IsSuccessStatus { get; set; }

    [RelayCommand]
    public async Task LoadProfileAsync()
    {
        if (IsBusy) return;

        try
        {
            IsBusy = true;
            HasStatusMessage = false;

            var profile = await _apiClient.GetMySchoolAsync();
            if (profile != null)
            {
                SchoolName = profile.Name;
                SchoolCode = profile.Code;
                Email = profile.Email;
                Phone = profile.Phone;
                Address = profile.Address;
                LogoUrl = profile.LogoUrl ?? string.Empty;
                ThemePrimaryColor = profile.ThemePrimaryColor ?? "#6C5CE7";
                ThemeSecondaryColor = profile.ThemeSecondaryColor ?? "#5840BA";
                ThemeDarkMode = profile.ThemeDarkMode;
                BranchesCount = profile.BranchesCount;
                UsersCount = profile.UsersCount;
            }
            else
            {
                StatusMessage = "Failed to load school profile.";
                HasStatusMessage = true;
                IsSuccessStatus = false;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading profile: {ex.Message}";
            HasStatusMessage = true;
            IsSuccessStatus = false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task SaveProfileAsync()
    {
        if (IsBusy) return;

        if (string.IsNullOrWhiteSpace(SchoolName))
        {
            StatusMessage = "School name cannot be empty.";
            HasStatusMessage = true;
            IsSuccessStatus = false;
            return;
        }

        try
        {
            IsBusy = true;
            HasStatusMessage = false;

            var request = new UpdateSchoolProfileRequestDto(
                SchoolName.Trim(),
                string.IsNullOrWhiteSpace(Email) ? null : Email.Trim(),
                string.IsNullOrWhiteSpace(Phone) ? null : Phone.Trim(),
                string.IsNullOrWhiteSpace(Address) ? null : Address.Trim(),
                string.IsNullOrWhiteSpace(LogoUrl) ? null : LogoUrl.Trim(),
                ThemePrimaryColor.Trim(),
                ThemeSecondaryColor.Trim(),
                ThemeDarkMode
            );

            var (success, message) = await _apiClient.UpdateMySchoolAsync(request);
            StatusMessage = message;
            HasStatusMessage = true;
            IsSuccessStatus = success;

            if (success)
            {
                // Reload profile to refresh stats and updated data
                await LoadProfileAsync();
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Save error: {ex.Message}";
            HasStatusMessage = true;
            IsSuccessStatus = false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public void SelectColor(string colorHex)
    {
        if (!string.IsNullOrWhiteSpace(colorHex))
        {
            ThemePrimaryColor = colorHex;
        }
    }

    [RelayCommand]
    public async Task GoBackAsync()
    {
        await Shell.Current.GoToAsync("..");
    }
}
