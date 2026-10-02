using SchoolManagement.Client.Maui.ViewModels;

namespace SchoolManagement.Client.Maui.Views;

public partial class SchoolProfilePage : ContentPage
{
    public SchoolProfilePage(SchoolProfileViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
