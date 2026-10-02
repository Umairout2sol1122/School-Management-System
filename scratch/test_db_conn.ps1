$connectionString = "Server=.;Database=SchoolManagementDb;User Id=sa;Password=out2sol;TrustServerCertificate=True;"
$connection = New-Object System.Data.SqlClient.SqlConnection($connectionString)
$connection.Open()
Write-Host "SQL Server Connected successfully. State: $($connection.State)" -ForegroundColor Green
$cmd = $connection.CreateCommand()
$cmd.CommandText = "SELECT COUNT(*) FROM sys.tables WHERE schema_name(schema_id) = 'Tenant'"
$count = $cmd.ExecuteScalar()
Write-Host "Tenant tables count: $count" -ForegroundColor Cyan
$connection.Close()
