using SchoolManagement.Client.Maui.ViewModels;

namespace SchoolManagement.Client.Maui.Views;

public partial class LoginPage : ContentPage
{
    public LoginPage(LoginViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
