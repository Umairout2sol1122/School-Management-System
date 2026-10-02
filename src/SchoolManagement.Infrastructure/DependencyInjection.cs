using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.Common.Models;
using SchoolManagement.Infrastructure.Configuration;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.Infrastructure.Security;
using SchoolManagement.Infrastructure.Services;

namespace SchoolManagement.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Bind configuration sections
        var jwtSettings = new JwtSettings();
        configuration.GetSection(JwtSettings.SectionName).Bind(jwtSettings);
        services.AddSingleton(jwtSettings);

        var syncSettings = new SyncSettings();
        configuration.GetSection(SyncSettings.SectionName).Bind(syncSettings);
        services.AddSingleton(syncSettings);

        // Database connection string setup
        string connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Server=.;Database=SchoolManagementDb;User Id=sa;Password=out2sol;TrustServerCertificate=True;MultipleActiveResultSets=True;";

        services.AddDbContext<ApplicationDbContext>((sp, options) =>
        {
            options.UseSqlServer(connectionString, sqlOptions =>
            {
                sqlOptions.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName);
                sqlOptions.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), errorNumbersToAdd: null);
            });
        });

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());
        services.AddScoped<ITenantContext, TenantContext>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<ITokenService, TokenService>();
        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
        services.AddScoped<IAuditService, AuditService>();

        // Payment Gateways
        services.AddScoped<IPaymentGatewayService, SchoolManagement.Infrastructure.Services.PaymentGateways.JazzCashPaymentService>();
        services.AddScoped<IPaymentGatewayService, SchoolManagement.Infrastructure.Services.PaymentGateways.EasyPaisaPaymentService>();
        services.AddScoped<IPaymentGatewayService, SchoolManagement.Infrastructure.Services.PaymentGateways.StripePaymentService>();
        services.AddScoped<IPaymentGatewayResolver, SchoolManagement.Infrastructure.Services.PaymentGateways.PaymentGatewayResolver>();

        // SMS Gateways & Notifications
        services.AddScoped<SchoolManagement.Infrastructure.Services.SmsGateways.JazzSmsService>();
        services.AddScoped<SchoolManagement.Infrastructure.Services.SmsGateways.TwilioSmsService>();
        services.AddScoped<ISmsService>(sp =>
        {
            string provider = configuration["Sms:Provider"] ?? "Jazz";
            return provider.Equals("Twilio", StringComparison.OrdinalIgnoreCase)
                ? sp.GetRequiredService<SchoolManagement.Infrastructure.Services.SmsGateways.TwilioSmsService>()
                : sp.GetRequiredService<SchoolManagement.Infrastructure.Services.SmsGateways.JazzSmsService>();
        });
        services.AddScoped<ISmsNotificationService, SchoolManagement.Infrastructure.Services.SmsGateways.SmsNotificationService>();

        // Certificates & Document Template Engine
        services.AddScoped<ICertificateService, SchoolManagement.Infrastructure.Services.Certificates.CertificateService>();

        // Staff Payroll Engine
        services.AddScoped<IPayrollService, SchoolManagement.Infrastructure.Services.Payroll.PayrollService>();

        return services;
    }
}
