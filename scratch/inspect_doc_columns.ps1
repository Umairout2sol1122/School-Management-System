$c = New-Object System.Data.SqlClient.SqlConnection('Server=.;Database=SchoolManagementDb;User Id=sa;Password=out2sol;TrustServerCertificate=True;')
$c.Open()
$cmd = $c.CreateCommand()
$cmd.CommandText = "SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = 'Tenant' AND TABLE_NAME = 'StudentDocuments'"
$r = $cmd.ExecuteReader()
while($r.Read()) { Write-Host "$($r[0]) | $($r[1]) | $($r[2]) | $($r[3])" }
$c.Close()
