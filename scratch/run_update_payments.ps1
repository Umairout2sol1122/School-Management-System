$c = New-Object System.Data.SqlClient.SqlConnection('Server=.;Database=SchoolManagementDb;User Id=sa;Password=out2sol;TrustServerCertificate=True;')
$c.Open()
$sql = Get-Content -Raw -Path 'scratch/update_feepayments_schema.sql'
$cmd = $c.CreateCommand()
$cmd.CommandText = $sql
$cmd.ExecuteNonQuery()
Write-Host "Schema updated successfully."
$c.Close()
