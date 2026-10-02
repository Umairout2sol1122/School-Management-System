using Serilog;
using Serilog.Events;
using Serilog.Formatting.Json;

namespace SchoolManagement.Infrastructure.Logging;

public static class SerilogConfiguration
{
    public static void ConfigureLogging(string logsDirectory = "logs")
    {
        Directory.CreateDirectory(logsDirectory);

        string textLogPath = Path.Combine(logsDirectory, "school-management-.log");
        string jsonLogPath = Path.Combine(logsDirectory, "school-management-structured-.json");

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", "SchoolManagementSystem")
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(
                path: textLogPath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{SourceContext}] [OrgId:{OrganizationId}] [UserId:{UserId}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(
                formatter: new JsonFormatter(),
                path: jsonLogPath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30)
            .CreateLogger();
    }
}
