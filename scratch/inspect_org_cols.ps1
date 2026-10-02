$connectionString = "Server=.;Database=SchoolManagementDb;User Id=sa;Password=out2sol;TrustServerCertificate=True;"
$conn = New-Object System.Data.SqlClient.SqlConnection($connectionString)
$conn.Open()
$cmd = $conn.CreateCommand()
$cmd.CommandText = "SELECT COLUMN_NAME, IS_NULLABLE, DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = 'Tenant' AND TABLE_NAME = 'Organizations'"
$r = $cmd.ExecuteReader()
while ($r.Read()) {
    Write-Host "$($r['COLUMN_NAME']) | Nullable: $($r['IS_NULLABLE']) | Type: $($r['DATA_TYPE'])"
}
$conn.Close()
