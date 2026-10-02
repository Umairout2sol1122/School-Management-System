$connectionString = "Server=.;Database=SchoolManagementDb;User Id=sa;Password=out2sol;TrustServerCertificate=True;"
$connection = New-Object System.Data.SqlClient.SqlConnection($connectionString)
$connection.Open()
$command = $connection.CreateCommand()
$command.CommandText = "SELECT TOP 8 Action, Module, EntityName, Details, TimestampUtc FROM [Audit].[AuditLogs] ORDER BY TimestampUtc DESC"
$reader = $command.ExecuteReader()
while ($reader.Read()) {
    Write-Host "$($reader['TimestampUtc']) | Action=$($reader['Action']) | Module=$($reader['Module']) | $($reader['Details'])" -ForegroundColor Cyan
}
$connection.Close()
