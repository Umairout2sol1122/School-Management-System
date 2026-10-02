using SchoolManagement.Client.Maui.ViewModels;

namespace SchoolManagement.Client.Maui.Views;

public partial class OrganizationsPage : ContentPage
{
    private readonly OrganizationsViewModel _viewModel;

    public OrganizationsPage(OrganizationsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadOrganizationsAsync();
    }
}
