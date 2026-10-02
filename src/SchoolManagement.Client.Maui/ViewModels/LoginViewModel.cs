using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolManagement.Application.Common.Models;
using SchoolManagement.Client.Core.Auth;
using SchoolManagement.Client.Core.Services;

namespace SchoolManagement.Client.Maui.ViewModels;

public partial class LoginViewModel : BaseViewModel
{
    private readonly IApiClient _apiClient;
    private readonly ClientAuthState _authState;

    public LoginViewModel(IApiClient apiClient, ClientAuthState authState)
    {
        _apiClient = apiClient;
        _authState = authState;
        Title = "Sign In — School Management";
    }

    [ObservableProperty]
    public partial string EmailOrUsername { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Password { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool RememberMe { get; set; } = true;

    [RelayCommand]
    public async Task LoginAsync()
    {
        if (IsBusy)
            return;

        ClearError();

        if (string.IsNullOrWhiteSpace(EmailOrUsername))
        {
            ErrorMessage = "Please enter your username or email.";
            return;
        }

        if (string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Please enter your password.";
            return;
        }

        try
        {
            IsBusy = true;

            var request = new LoginRequestDto(EmailOrUsername.Trim(), Password);
            var result = await _apiClient.LoginAsync(request);

            if (!result.Success || result.User == null || string.IsNullOrWhiteSpace(result.AccessToken))
            {
                ErrorMessage = result.ErrorMessage ?? "Invalid credentials. Please verify your login details.";
                return;
            }

            _apiClient.SetAuthToken(result.AccessToken);
            _authState.SetAuthenticated(
                result.User,
                result.AccessToken,
                result.RefreshToken ?? string.Empty,
                result.ExpiresAtUtc ?? DateTime.UtcNow.AddHours(1));

            // Navigate to Dashboard
            await Shell.Current.GoToAsync("//DashboardPage");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Authentication error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
