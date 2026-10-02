$c = New-Object System.Data.SqlClient.SqlConnection('Server=.;Database=SchoolManagementDb;User Id=sa;Password=out2sol;TrustServerCertificate=True;')
$c.Open()
$cmd = $c.CreateCommand()
$cmd.CommandText = 'SELECT MigrationId FROM dbo.__EFMigrationsHistory'
$r = $cmd.ExecuteReader()
while($r.Read()) { Write-Host $r[0] }
$c.Close()
