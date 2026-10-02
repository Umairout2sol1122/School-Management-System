$c = New-Object System.Data.SqlClient.SqlConnection('Server=.;Database=SchoolManagementDb;User Id=sa;Password=out2sol;TrustServerCertificate=True;')
$c.Open()
$sql = Get-Content -Raw -Path 'scratch/create_gap_tables.sql'
$cmd = $c.CreateCommand()
$cmd.CommandText = $sql
$cmd.ExecuteNonQuery()
Write-Host "Tables created successfully."
$c.Close()
