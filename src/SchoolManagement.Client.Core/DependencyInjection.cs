using Microsoft.Extensions.DependencyInjection;
using SchoolManagement.Client.Core.Auth;
using SchoolManagement.Client.Core.Persistence;
using SchoolManagement.Client.Core.Services;
using SchoolManagement.Client.Core.Sync;

namespace SchoolManagement.Client.Core;

public static class DependencyInjection
{
    public static IServiceCollection AddClientCore(
        this IServiceCollection services,
        string sqliteDbPath,
        string serverBaseUrl = "https://localhost:7001/")
    {
        services.AddSingleton(new LocalDbContext(sqliteDbPath));
        services.AddSingleton<ClientAuthState>();

        services.AddHttpClient<IApiClient, ApiClient>(client =>
        {
            client.BaseAddress = new Uri(serverBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        services.AddScoped<ISyncEngine, SyncEngine>();

        return services;
    }
}
