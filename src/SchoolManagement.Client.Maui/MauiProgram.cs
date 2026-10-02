using Microsoft.Extensions.Logging;
using SchoolManagement.Client.Core;
using SchoolManagement.Client.Maui.ViewModels;
using SchoolManagement.Client.Maui.Views;

namespace SchoolManagement.Client.Maui;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // Setup local SQLite database path for offline resilience
        string localDbPath = Path.Combine(FileSystem.AppDataDirectory, "school_local_offline.db");
        builder.Services.AddClientCore(localDbPath, "https://localhost:7001/");

        // Register ViewModels
        builder.Services.AddTransient<LoginViewModel>();
        builder.Services.AddTransient<DashboardViewModel>();
        builder.Services.AddTransient<OrganizationsViewModel>();
        builder.Services.AddTransient<SchoolProfileViewModel>();
        builder.Services.AddTransient<StudentsViewModel>();
        builder.Services.AddTransient<StaffViewModel>();

        // Register Views
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<DashboardPage>();
        builder.Services.AddTransient<OrganizationsPage>();
        builder.Services.AddTransient<SchoolProfilePage>();
        builder.Services.AddTransient<StudentsPage>();
        builder.Services.AddTransient<StaffPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
