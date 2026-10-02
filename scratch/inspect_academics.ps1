$connectionString = "Server=.;Database=SchoolManagementDb;User Id=sa;Password=out2sol;TrustServerCertificate=True;"
$conn = New-Object System.Data.SqlClient.SqlConnection($connectionString)
$conn.Open()

Write-Host "--- Academic Sessions ---"
$cmd = $conn.CreateCommand()
$cmd.CommandText = "SELECT TOP 5 Id, OrganizationId, Name, IsCurrent FROM Tenant.AcademicSessions WHERE IsDeleted = 0"
$r = $cmd.ExecuteReader()
while ($r.Read()) { Write-Host "$($r['Id']) | Org: $($r['OrganizationId']) | $($r['Name']) | Current: $($r['IsCurrent'])" }
$r.Close()

Write-Host "`n--- Class Grades ---"
$cmd.CommandText = "SELECT TOP 5 Id, OrganizationId, BranchId, Name FROM Tenant.ClassGrades WHERE IsDeleted = 0"
$r = $cmd.ExecuteReader()
while ($r.Read()) { Write-Host "$($r['Id']) | Org: $($r['OrganizationId']) | Branch: $($r['BranchId']) | $($r['Name'])" }
$r.Close()

Write-Host "`n--- Class Sections ---"
$cmd.CommandText = "SELECT TOP 5 Id, OrganizationId, BranchId, ClassGradeId, Name FROM Tenant.ClassSections WHERE IsDeleted = 0"
$r = $cmd.ExecuteReader()
while ($r.Read()) { Write-Host "$($r['Id']) | Org: $($r['OrganizationId']) | Grade: $($r['ClassGradeId']) | $($r['Name'])" }
$r.Close()

Write-Host "`n--- Subject Courses ---"
$cmd.CommandText = "SELECT TOP 5 Id, OrganizationId, BranchId, Name, Code FROM Tenant.SubjectCourses WHERE IsDeleted = 0"
$r = $cmd.ExecuteReader()
while ($r.Read()) { Write-Host "$($r['Id']) | Org: $($r['OrganizationId']) | $($r['Name']) ($($r['Code']))" }
$r.Close()

$conn.Close()
