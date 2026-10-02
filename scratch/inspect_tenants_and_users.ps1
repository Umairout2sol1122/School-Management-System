$connectionString = "Server=.;Database=SchoolManagementDb;User Id=sa;Password=out2sol;TrustServerCertificate=True;MultipleActiveResultSets=True;"
$conn = New-Object System.Data.SqlClient.SqlConnection($connectionString)
$conn.Open()

Write-Host "--- Organizations ---"
$cmd = $conn.CreateCommand()
$cmd.CommandText = "SELECT Id, Name, Code, IsActive FROM Tenant.Organizations WHERE IsDeleted = 0"
$reader = $cmd.ExecuteReader()
while ($reader.Read()) {
    Write-Host "$($reader['Id']) | $($reader['Name']) | $($reader['Code']) | Active: $($reader['IsActive'])"
}
$reader.Close()

Write-Host "`n--- Branches ---"
$cmd.CommandText = "SELECT Id, OrganizationId, Name, Code, IsActive FROM Tenant.Branches WHERE IsDeleted = 0"
$reader = $cmd.ExecuteReader()
while ($reader.Read()) {
    Write-Host "$($reader['Id']) | Org: $($reader['OrganizationId']) | $($reader['Name']) | $($reader['Code'])"
}
$reader.Close()

Write-Host "`n--- Staff / Teachers ---"
$cmd.CommandText = "SELECT TOP 10 sp.Id, sp.OrganizationId, sp.BranchId, sp.EmployeeId, sp.Designation, u.Email, u.Username, u.FullName, u.PasswordHash, u.RoleType FROM Tenant.StaffProfiles sp INNER JOIN Security.Users u ON sp.UserId = u.Id WHERE sp.IsDeleted = 0"
$reader = $cmd.ExecuteReader()
while ($reader.Read()) {
    Write-Host "$($reader['Id']) | Org: $($reader['OrganizationId']) | $($reader['FullName']) ($($reader['Username']) / $($reader['Email'])) | $($reader['Designation']) | RoleType: $($reader['RoleType'])"
}
$reader.Close()

Write-Host "`n--- Students ---"
$cmd.CommandText = "SELECT TOP 10 s.Id, s.OrganizationId, s.BranchId, s.AdmissionNumber, s.FirstName, s.LastName, u.Email, u.Username, u.RoleType FROM Student.Students s LEFT JOIN Security.Users u ON s.UserId = u.Id WHERE s.IsDeleted = 0"
$reader = $cmd.ExecuteReader()
while ($reader.Read()) {
    Write-Host "$($reader['Id']) | Org: $($reader['OrganizationId']) | $($reader['FirstName']) $($reader['LastName']) ($($reader['Username']) / $($reader['Email'])) | Adm: $($reader['AdmissionNumber']) | RoleType: $($reader['RoleType'])"
}
$reader.Close()

$conn.Close()
