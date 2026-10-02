using SchoolManagement.Client.Maui.ViewModels;

namespace SchoolManagement.Client.Maui.Views;

public partial class StaffPage : ContentPage
{
    private readonly StaffViewModel _viewModel;

    public StaffPage(StaffViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadStaffAsync();
    }
}
