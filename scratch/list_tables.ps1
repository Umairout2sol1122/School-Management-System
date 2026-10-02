$connectionString = "Server=.;Database=SchoolManagementDb;User Id=sa;Password=out2sol;TrustServerCertificate=True;"
$conn = New-Object System.Data.SqlClient.SqlConnection($connectionString)
$conn.Open()
$cmd = $conn.CreateCommand()
$cmd.CommandText = "SELECT SCHEMA_NAME(schema_id) + '.' + name FROM sys.tables ORDER BY 1"
$reader = $cmd.ExecuteReader()
while ($reader.Read()) {
    Write-Host $reader[0]
}
$conn.Close()
