namespace SchoolManagement.Infrastructure.Configuration;

public class JwtSettings
{
    public const string SectionName = "JwtSettings";

    public string Secret { get; set; } = "Pak2Source_SuperSecretMasterKey_2026_SchoolManagementSystem_32bytes_min!";
    public string Issuer { get; set; } = "SchoolManagementPlatform";
    public string Audience { get; set; } = "SchoolManagementClients";
    public int ExpiryMinutes { get; set; } = 60;
    public int RefreshTokenExpiryDays { get; set; } = 30;
}

public class DatabaseSettings
{
    public const string SectionName = "DatabaseSettings";

    public string ConnectionString { get; set; } = string.Empty;
}

public class SyncSettings
{
    public const string SectionName = "SyncSettings";

    public int BatchSize { get; set; } = 50;
    public int MaxRetryAttempts { get; set; } = 5;
    public int SyncIntervalSeconds { get; set; } = 30;
}
