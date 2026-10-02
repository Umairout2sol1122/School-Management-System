$connectionString = "Server=.;Database=SchoolManagementDb;User Id=sa;Password=out2sol;TrustServerCertificate=True;MultipleActiveResultSets=True;"
$conn = New-Object System.Data.SqlClient.SqlConnection($connectionString)
$conn.Open()

function Hash-Password($pwd) {
    $saltBytes = [System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32)
    $salt = [Convert]::ToBase64String($saltBytes)
    $passBytes = [System.Text.Encoding]::UTF8.GetBytes($pwd)
    $hashBytes = [System.Security.Cryptography.Rfc2898DeriveBytes]::Pbkdf2($passBytes, $saltBytes, 100000, [System.Security.Cryptography.HashAlgorithmName]::SHA512, 64)
    $hash = [Convert]::ToBase64String($hashBytes)
    return @{ Hash = $hash; Salt = $salt }
}

function Execute-Sql($sql, $params = @{}) {
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = $sql
    foreach ($k in $params.Keys) {
        $p = $cmd.CreateParameter()
        $p.ParameterName = "@$k"
        $val = $params[$k]
        if ($val -eq $null) { $p.Value = [DBNull]::Value } else { $p.Value = $val }
        $cmd.Parameters.Add($p) | Out-Null
    }
    return $cmd.ExecuteNonQuery()
}

function Query-Scalar($sql, $params = @{}) {
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = $sql
    foreach ($k in $params.Keys) {
        $p = $cmd.CreateParameter()
        $p.ParameterName = "@$k"
        $val = $params[$k]
        if ($val -eq $null) { $p.Value = [DBNull]::Value } else { $p.Value = $val }
        $cmd.Parameters.Add($p) | Out-Null
    }
    return $cmd.ExecuteScalar()
}

Write-Host "Setting up multi-organization test environment..." -ForegroundColor Cyan

# 1. Organization A & B IDs
$orgAId = [Guid]"11111111-1111-1111-1111-111111111111"
$orgBId = [Guid]"22222222-2222-2222-2222-222222222222"

# Clean up any existing test records with these IDs in proper foreign key order
Execute-Sql "DELETE FROM Tenant.AssignmentSubmissions WHERE OrganizationId IN (@A, @B)" @{ A=$orgAId; B=$orgBId }
Execute-Sql "DELETE FROM Tenant.HomeworkAssignments WHERE OrganizationId IN (@A, @B)" @{ A=$orgAId; B=$orgBId }
Execute-Sql "DELETE FROM Tenant.ExamResults WHERE OrganizationId IN (@A, @B)" @{ A=$orgAId; B=$orgBId }
Execute-Sql "DELETE FROM Tenant.ExamSchedules WHERE OrganizationId IN (@A, @B)" @{ A=$orgAId; B=$orgBId }
Execute-Sql "DELETE FROM Tenant.AttendanceRecords WHERE OrganizationId IN (@A, @B)" @{ A=$orgAId; B=$orgBId }
Execute-Sql "DELETE FROM Tenant.TimetableSlots WHERE OrganizationId IN (@A, @B)" @{ A=$orgAId; B=$orgBId }
Execute-Sql "DELETE FROM Tenant.LeaveRequests WHERE OrganizationId IN (@A, @B)" @{ A=$orgAId; B=$orgBId }
Execute-Sql "DELETE FROM Tenant.FeePayments WHERE OrganizationId IN (@A, @B)" @{ A=$orgAId; B=$orgBId }
Execute-Sql "DELETE FROM Tenant.FeeInvoices WHERE OrganizationId IN (@A, @B)" @{ A=$orgAId; B=$orgBId }
Execute-Sql "DELETE FROM Tenant.StudentDocuments WHERE OrganizationId IN (@A, @B)" @{ A=$orgAId; B=$orgBId }
Execute-Sql "DELETE FROM Tenant.CourseAssignments WHERE OrganizationId IN (@A, @B)" @{ A=$orgAId; B=$orgBId }
Execute-Sql "DELETE FROM Tenant.Announcements WHERE OrganizationId IN (@A, @B)" @{ A=$orgAId; B=$orgBId }
Execute-Sql "DELETE FROM Student.StudentGuardians WHERE OrganizationId IN (@A, @B)" @{ A=$orgAId; B=$orgBId }
Execute-Sql "DELETE FROM Student.Guardians WHERE OrganizationId IN (@A, @B)" @{ A=$orgAId; B=$orgBId }
Execute-Sql "DELETE FROM Student.Students WHERE OrganizationId IN (@A, @B)" @{ A=$orgAId; B=$orgBId }
Execute-Sql "DELETE FROM Staff.StaffProfiles WHERE OrganizationId IN (@A, @B)" @{ A=$orgAId; B=$orgBId }
Execute-Sql "DELETE FROM Security.UserRoles WHERE UserId IN (SELECT Id FROM Security.Users WHERE OrganizationId IN (@A, @B))" @{ A=$orgAId; B=$orgBId }
Execute-Sql "DELETE FROM Security.Users WHERE OrganizationId IN (@A, @B)" @{ A=$orgAId; B=$orgBId }
Execute-Sql "DELETE FROM Tenant.SubjectCourses WHERE OrganizationId IN (@A, @B)" @{ A=$orgAId; B=$orgBId }
Execute-Sql "DELETE FROM Tenant.ClassSections WHERE OrganizationId IN (@A, @B)" @{ A=$orgAId; B=$orgBId }
Execute-Sql "DELETE FROM Tenant.ClassGrades WHERE OrganizationId IN (@A, @B)" @{ A=$orgAId; B=$orgBId }
Execute-Sql "DELETE FROM Tenant.AcademicSessions WHERE OrganizationId IN (@A, @B)" @{ A=$orgAId; B=$orgBId }
Execute-Sql "DELETE FROM Tenant.Branches WHERE OrganizationId IN (@A, @B)" @{ A=$orgAId; B=$orgBId }
Execute-Sql "DELETE FROM Tenant.OrganizationModules WHERE OrganizationId IN (@A, @B)" @{ A=$orgAId; B=$orgBId }
Execute-Sql "DELETE FROM Tenant.Organizations WHERE Id IN (@A, @B)" @{ A=$orgAId; B=$orgBId }

# Insert Org A
Execute-Sql "INSERT INTO Tenant.Organizations (Id, Name, Code, Email, Phone, IsActive, CreatedBy, CreatedAtUtc, SyncVersion, IsDeleted)
VALUES (@Id, 'Apex International Academy', 'APEX-ORGA', 'info@apexacademy.com', '1234567890', 1, 'System', GETUTCDATE(), 1, 0)" @{ Id=$orgAId }

# Insert Org B
Execute-Sql "INSERT INTO Tenant.Organizations (Id, Name, Code, Email, Phone, IsActive, CreatedBy, CreatedAtUtc, SyncVersion, IsDeleted)
VALUES (@Id, 'Beacon Heights Institute', 'BEACON-ORGB', 'info@beaconheights.com', '0987654321', 1, 'System', GETUTCDATE(), 1, 0)" @{ Id=$orgBId }

# Branches for Org A
$branchA1Id = [Guid]"11111111-aaaa-1111-1111-111111111111"
$branchA2Id = [Guid]"11111111-bbbb-1111-1111-111111111111"
Execute-Sql "INSERT INTO Tenant.Branches (Id, OrganizationId, Name, Code, Address, City, IsActive, CreatedBy, CreatedAtUtc, SyncVersion, IsDeleted)
VALUES (@Id, @OrgId, 'Apex Main Campus', 'APEX-A1', '100 Main St', 'Capital City', 1, 'System', GETUTCDATE(), 1, 0)" @{ Id=$branchA1Id; OrgId=$orgAId }
Execute-Sql "INSERT INTO Tenant.Branches (Id, OrganizationId, Name, Code, Address, City, IsActive, CreatedBy, CreatedAtUtc, SyncVersion, IsDeleted)
VALUES (@Id, @OrgId, 'Apex North Campus', 'APEX-A2', '200 North Rd', 'Capital City', 1, 'System', GETUTCDATE(), 1, 0)" @{ Id=$branchA2Id; OrgId=$orgAId }

# Branch for Org B
$branchB1Id = [Guid]"22222222-aaaa-2222-2222-222222222222"
Execute-Sql "INSERT INTO Tenant.Branches (Id, OrganizationId, Name, Code, Address, City, IsActive, CreatedBy, CreatedAtUtc, SyncVersion, IsDeleted)
VALUES (@Id, @OrgId, 'Beacon Main Campus', 'BEACON-B1', '50 Beacon Ave', 'Metro City', 1, 'System', GETUTCDATE(), 1, 0)" @{ Id=$branchB1Id; OrgId=$orgBId }

# Academic Sessions
$sessionAId = [Guid]"11111111-cccc-1111-1111-111111111111"
$sessionBId = [Guid]"22222222-cccc-2222-2222-222222222222"
Execute-Sql "INSERT INTO Tenant.AcademicSessions (Id, OrganizationId, BranchId, Name, StartDate, EndDate, IsCurrent, IsActive, CreatedBy, CreatedAtUtc, SyncVersion, IsDeleted)
VALUES (@Id, @OrgId, @BranchId, '2026-2027 Academic Year', '2026-08-01', '2027-06-30', 1, 1, 'System', GETUTCDATE(), 1, 0)" @{ Id=$sessionAId; OrgId=$orgAId; BranchId=$branchA1Id }
Execute-Sql "INSERT INTO Tenant.AcademicSessions (Id, OrganizationId, BranchId, Name, StartDate, EndDate, IsCurrent, IsActive, CreatedBy, CreatedAtUtc, SyncVersion, IsDeleted)
VALUES (@Id, @OrgId, @BranchId, '2026-2027 Academic Year', '2026-08-01', '2027-06-30', 1, 1, 'System', GETUTCDATE(), 1, 0)" @{ Id=$sessionBId; OrgId=$orgBId; BranchId=$branchB1Id }

# Class Grades
$grade8AId = [Guid]"11111111-dddd-1111-1111-111111111111" # Org A, Branch A1
$grade9AId = [Guid]"11111111-eeee-1111-1111-111111111111" # Org A, Branch A2
$grade8BId = [Guid]"22222222-dddd-2222-2222-222222222222" # Org B, Branch B1
Execute-Sql "INSERT INTO Tenant.ClassGrades (Id, OrganizationId, BranchId, Name, NumericOrder, IsActive, CreatedBy, CreatedAtUtc, SyncVersion, IsDeleted)
VALUES (@Id, @OrgId, @BranchId, 'Grade 8', 8, 1, 'System', GETUTCDATE(), 1, 0)" @{ Id=$grade8AId; OrgId=$orgAId; BranchId=$branchA1Id }
Execute-Sql "INSERT INTO Tenant.ClassGrades (Id, OrganizationId, BranchId, Name, NumericOrder, IsActive, CreatedBy, CreatedAtUtc, SyncVersion, IsDeleted)
VALUES (@Id, @OrgId, @BranchId, 'Grade 9', 9, 1, 'System', GETUTCDATE(), 1, 0)" @{ Id=$grade9AId; OrgId=$orgAId; BranchId=$branchA2Id }
Execute-Sql "INSERT INTO Tenant.ClassGrades (Id, OrganizationId, BranchId, Name, NumericOrder, IsActive, CreatedBy, CreatedAtUtc, SyncVersion, IsDeleted)
VALUES (@Id, @OrgId, @BranchId, 'Grade 8', 8, 1, 'System', GETUTCDATE(), 1, 0)" @{ Id=$grade8BId; OrgId=$orgBId; BranchId=$branchB1Id }

# Class Sections
$sec8A1Id = [Guid]"11111111-ffff-1111-1111-111111111111" # Grade 8 Section A (Branch A1)
$sec8A2Id = [Guid]"11111111-ffff-2222-1111-111111111111" # Grade 8 Section B (Branch A1)
$sec9A1Id = [Guid]"11111111-ffff-3333-1111-111111111111" # Grade 9 Section A (Branch A2)
$sec8B1Id = [Guid]"22222222-ffff-1111-2222-222222222222" # Grade 8 Section A (Branch B1)
Execute-Sql "INSERT INTO Tenant.ClassSections (Id, OrganizationId, BranchId, ClassGradeId, Name, RoomNumber, Capacity, IsActive, CreatedBy, CreatedAtUtc, SyncVersion, IsDeleted)
VALUES (@Id, @OrgId, @BranchId, @GradeId, 'Section A', 'Room 101', 35, 1, 'System', GETUTCDATE(), 1, 0)" @{ Id=$sec8A1Id; OrgId=$orgAId; BranchId=$branchA1Id; GradeId=$grade8AId }
Execute-Sql "INSERT INTO Tenant.ClassSections (Id, OrganizationId, BranchId, ClassGradeId, Name, RoomNumber, Capacity, IsActive, CreatedBy, CreatedAtUtc, SyncVersion, IsDeleted)
VALUES (@Id, @OrgId, @BranchId, @GradeId, 'Section B', 'Room 102', 35, 1, 'System', GETUTCDATE(), 1, 0)" @{ Id=$sec8A2Id; OrgId=$orgAId; BranchId=$branchA1Id; GradeId=$grade8AId }
Execute-Sql "INSERT INTO Tenant.ClassSections (Id, OrganizationId, BranchId, ClassGradeId, Name, RoomNumber, Capacity, IsActive, CreatedBy, CreatedAtUtc, SyncVersion, IsDeleted)
VALUES (@Id, @OrgId, @BranchId, @GradeId, 'Section A', 'Room 201', 35, 1, 'System', GETUTCDATE(), 1, 0)" @{ Id=$sec9A1Id; OrgId=$orgAId; BranchId=$branchA2Id; GradeId=$grade9AId }
Execute-Sql "INSERT INTO Tenant.ClassSections (Id, OrganizationId, BranchId, ClassGradeId, Name, RoomNumber, Capacity, IsActive, CreatedBy, CreatedAtUtc, SyncVersion, IsDeleted)
VALUES (@Id, @OrgId, @BranchId, @GradeId, 'Section A', 'Room 101', 35, 1, 'System', GETUTCDATE(), 1, 0)" @{ Id=$sec8B1Id; OrgId=$orgBId; BranchId=$branchB1Id; GradeId=$grade8BId }

# Subjects
$subjMathAId = [Guid]"11111111-1234-1111-1111-111111111111"
$subjSciAId  = [Guid]"11111111-1234-2222-1111-111111111111"
$subjMathBId = [Guid]"22222222-1234-1111-2222-222222222222"
Execute-Sql "INSERT INTO Tenant.SubjectCourses (Id, OrganizationId, BranchId, Name, Code, IsActive, CreatedBy, CreatedAtUtc, SyncVersion, IsDeleted)
VALUES (@Id, @OrgId, @BranchId, 'Mathematics', 'MATH-8', 1, 'System', GETUTCDATE(), 1, 0)" @{ Id=$subjMathAId; OrgId=$orgAId; BranchId=$branchA1Id }
Execute-Sql "INSERT INTO Tenant.SubjectCourses (Id, OrganizationId, BranchId, Name, Code, IsActive, CreatedBy, CreatedAtUtc, SyncVersion, IsDeleted)
VALUES (@Id, @OrgId, @BranchId, 'Science', 'SCI-9', 1, 'System', GETUTCDATE(), 1, 0)" @{ Id=$subjSciAId; OrgId=$orgAId; BranchId=$branchA2Id }
Execute-Sql "INSERT INTO Tenant.SubjectCourses (Id, OrganizationId, BranchId, Name, Code, IsActive, CreatedBy, CreatedAtUtc, SyncVersion, IsDeleted)
VALUES (@Id, @OrgId, @BranchId, 'Mathematics', 'MATH-8B', 1, 'System', GETUTCDATE(), 1, 0)" @{ Id=$subjMathBId; OrgId=$orgBId; BranchId=$branchB1Id }

# Get or create System Roles
$teacherRole = Query-Scalar "SELECT Id FROM Security.Roles WHERE Name = 'Teacher' AND IsSystemRole = 1"
if (-not $teacherRole) {
    $teacherRoleId = [Guid]::NewGuid()
    Execute-Sql "INSERT INTO Security.Roles (Id, Name, NormalizedName, Description, IsSystemRole, RoleType, CreatedBy, CreatedAtUtc, SyncVersion, IsDeleted)
    VALUES (@Id, 'Teacher', 'TEACHER', 'Teacher System Role', 1, 4, 'System', GETUTCDATE(), 1, 0)" @{ Id=$teacherRoleId }
} else {
    $teacherRoleId = [Guid]$teacherRole
}

$studentRole = Query-Scalar "SELECT Id FROM Security.Roles WHERE Name = 'Student' AND IsSystemRole = 1"
if (-not $studentRole) {
    $studentRoleId = [Guid]::NewGuid()
    Execute-Sql "INSERT INTO Security.Roles (Id, Name, NormalizedName, Description, IsSystemRole, RoleType, CreatedBy, CreatedAtUtc, SyncVersion, IsDeleted)
    VALUES (@Id, 'Student', 'STUDENT', 'Student System Role', 1, 5, 'System', GETUTCDATE(), 1, 0)" @{ Id=$studentRoleId }
} else {
    $studentRoleId = [Guid]$studentRole
}

$orgAdminRole = Query-Scalar "SELECT Id FROM Security.Roles WHERE Name = 'Organization Admin' AND IsSystemRole = 1"
if (-not $orgAdminRole) {
    $orgAdminRoleId = [Guid]::NewGuid()
    Execute-Sql "INSERT INTO Security.Roles (Id, Name, NormalizedName, Description, IsSystemRole, RoleType, CreatedBy, CreatedAtUtc, SyncVersion, IsDeleted)
    VALUES (@Id, 'Organization Admin', 'ORGANIZATION ADMIN', 'Organization Admin Role', 1, 2, 'System', GETUTCDATE(), 1, 0)" @{ Id=$orgAdminRoleId }
} else {
    $orgAdminRoleId = [Guid]$orgAdminRole
}

# Helper to create user
function Create-User($userId, $orgId, $branchId, $email, $username, $fullName, $roleType, $roleId) {
    $pwd = Hash-Password "Password@123"
    Execute-Sql "INSERT INTO Security.Users (Id, OrganizationId, BranchId, FullName, Email, NormalizedEmail, Username, NormalizedUsername, PasswordHash, PasswordSalt, RoleType, IsActive, EmailConfirmed, CreatedBy, CreatedAtUtc, SyncVersion, IsDeleted)
    VALUES (@Id, @OrgId, @BranchId, @FullName, @Email, UPPER(@Email), @Username, UPPER(@Username), @Hash, @Salt, @RoleType, 1, 1, 'System', GETUTCDATE(), 1, 0)" @{
        Id=$userId; OrgId=$orgId; BranchId=$branchId; FullName=$fullName; Email=$email; Username=$username; Hash=$pwd.Hash; Salt=$pwd.Salt; RoleType=$roleType
    }
    Execute-Sql "INSERT INTO Security.UserRoles (Id, UserId, RoleId, CreatedBy, CreatedAtUtc, SyncVersion, IsDeleted)
    VALUES (NEWID(), @UserId, @RoleId, 'System', GETUTCDATE(), 1, 0)" @{ UserId=$userId; RoleId=$roleId }
}

# Users
$orgAAdminId = [Guid]"11111111-9999-1111-1111-111111111111"
$teacherA1Id = [Guid]"11111111-8888-1111-1111-111111111111"
$teacherA2Id = [Guid]"11111111-8888-2222-1111-111111111111"
$studentA1Id = [Guid]"11111111-7777-1111-1111-111111111111"
$studentA2Id = [Guid]"11111111-7777-2222-1111-111111111111"

$orgBAdminId = [Guid]"22222222-9999-2222-2222-222222222222"
$teacherB1Id = [Guid]"22222222-8888-1111-2222-222222222222"
$studentB1Id = [Guid]"22222222-7777-1111-2222-222222222222"

Create-User $orgAAdminId $orgAId $branchA1Id "orga_admin@test.com" "orga_admin" "OrgA Admin" 2 $orgAdminRoleId
Create-User $teacherA1Id $orgAId $branchA1Id "teacherA1@test.com" "teacherA1" "Tariq Mahmood (Teacher A1)" 4 $teacherRoleId
Create-User $teacherA2Id $orgAId $branchA2Id "teacherA2@test.com" "teacherA2" "Sadia Khan (Teacher A2)" 4 $teacherRoleId
Create-User $studentA1Id $orgAId $branchA1Id "studentA1@test.com" "studentA1" "Ali Raza (Student A1)" 5 $studentRoleId
Create-User $studentA2Id $orgAId $branchA1Id "studentA2@test.com" "studentA2" "Bilal Ahmed (Student A2)" 5 $studentRoleId

Create-User $orgBAdminId $orgBId $branchB1Id "orgb_admin@test.com" "orgb_admin" "OrgB Admin" 2 $orgAdminRoleId
Create-User $teacherB1Id $orgBId $branchB1Id "teacherB1@test.com" "teacherB1" "Usman Qureshi (Teacher B1)" 4 $teacherRoleId
Create-User $studentB1Id $orgBId $branchB1Id "studentB1@test.com" "studentB1" "Fatima Noor (Student B1)" 5 $studentRoleId

# Staff Profiles
$staffA1ProfileId = [Guid]"11111111-8888-1111-9999-111111111111"
$staffA2ProfileId = [Guid]"11111111-8888-2222-9999-111111111111"
$staffB1ProfileId = [Guid]"22222222-8888-1111-9999-222222222222"

Execute-Sql "INSERT INTO Staff.StaffProfiles (Id, OrganizationId, BranchId, UserId, EmployeeCode, Designation, Department, Qualification, Specialization, EmploymentType, Status, JoiningDate, BasicSalary, CreatedBy, CreatedAtUtc, SyncVersion, IsDeleted)
VALUES (@Id, @OrgId, @BranchId, @UserId, 'EMP-A1-001', 'Senior Mathematics Teacher', 'Academics', 'M.Sc Mathematics', 'Algebra & Geometry', 1, 1, '2023-01-15', 75000, 'System', GETUTCDATE(), 1, 0)" @{ Id=$staffA1ProfileId; OrgId=$orgAId; BranchId=$branchA1Id; UserId=$teacherA1Id }

Execute-Sql "INSERT INTO Staff.StaffProfiles (Id, OrganizationId, BranchId, UserId, EmployeeCode, Designation, Department, Qualification, Specialization, EmploymentType, Status, JoiningDate, BasicSalary, CreatedBy, CreatedAtUtc, SyncVersion, IsDeleted)
VALUES (@Id, @OrgId, @BranchId, @UserId, 'EMP-A2-001', 'Science Instructor', 'Science', 'M.Sc Physics', 'Physics', 1, 1, '2023-08-20', 70000, 'System', GETUTCDATE(), 1, 0)" @{ Id=$staffA2ProfileId; OrgId=$orgAId; BranchId=$branchA2Id; UserId=$teacherA2Id }

Execute-Sql "INSERT INTO Staff.StaffProfiles (Id, OrganizationId, BranchId, UserId, EmployeeCode, Designation, Department, Qualification, Specialization, EmploymentType, Status, JoiningDate, BasicSalary, CreatedBy, CreatedAtUtc, SyncVersion, IsDeleted)
VALUES (@Id, @OrgId, @BranchId, @UserId, 'EMP-B1-001', 'Mathematics Faculty', 'Academics', 'M.Sc Mathematics', 'Applied Math', 1, 1, '2024-02-10', 80000, 'System', GETUTCDATE(), 1, 0)" @{ Id=$staffB1ProfileId; OrgId=$orgBId; BranchId=$branchB1Id; UserId=$teacherB1Id }

# Students
$studRecordA1Id = [Guid]"11111111-6666-1111-1111-111111111111"
$studRecordA2Id = [Guid]"11111111-6666-2222-1111-111111111111"
$studRecordB1Id = [Guid]"22222222-6666-1111-2222-222222222222"

Execute-Sql "INSERT INTO Student.Students (Id, OrganizationId, BranchId, AdmissionNumber, RollNumber, FirstName, LastName, DateOfBirth, Gender, AdmissionDate, Status, CurrentGrade, Section, Address, City, UserId, CreatedBy, CreatedAtUtc, SyncVersion, IsDeleted)
VALUES (@Id, @OrgId, @BranchId, 'ADM-A1-001', '8A-01', 'Ali', 'Raza', '2012-05-14', 1, '2026-08-01', 1, 'Grade 8', 'Section A', '12 Street A', 'Capital City', @UserId, 'System', GETUTCDATE(), 1, 0)" @{ Id=$studRecordA1Id; OrgId=$orgAId; BranchId=$branchA1Id; UserId=$studentA1Id }

Execute-Sql "INSERT INTO Student.Students (Id, OrganizationId, BranchId, AdmissionNumber, RollNumber, FirstName, LastName, DateOfBirth, Gender, AdmissionDate, Status, CurrentGrade, Section, Address, City, UserId, CreatedBy, CreatedAtUtc, SyncVersion, IsDeleted)
VALUES (@Id, @OrgId, @BranchId, 'ADM-A1-002', '8B-01', 'Bilal', 'Ahmed', '2012-09-20', 1, '2026-08-01', 1, 'Grade 8', 'Section B', '34 Street B', 'Capital City', @UserId, 'System', GETUTCDATE(), 1, 0)" @{ Id=$studRecordA2Id; OrgId=$orgAId; BranchId=$branchA1Id; UserId=$studentA2Id }

Execute-Sql "INSERT INTO Student.Students (Id, OrganizationId, BranchId, AdmissionNumber, RollNumber, FirstName, LastName, DateOfBirth, Gender, AdmissionDate, Status, CurrentGrade, Section, Address, City, UserId, CreatedBy, CreatedAtUtc, SyncVersion, IsDeleted)
VALUES (@Id, @OrgId, @BranchId, 'ADM-B1-001', '8A-01', 'Fatima', 'Noor', '2012-03-10', 2, '2026-08-01', 1, 'Grade 8', 'Section A', '56 Beacon Rd', 'Metro City', @UserId, 'System', GETUTCDATE(), 1, 0)" @{ Id=$studRecordB1Id; OrgId=$orgBId; BranchId=$branchB1Id; UserId=$studentB1Id }

# Course Assignments (Teacher A1 -> Grade 8 Section A Math; Teacher A2 -> Grade 9 Section A Science; Teacher B1 -> Grade 8 Section A Math B)
$caTeacherA1Id = [Guid]"11111111-5555-1111-1111-111111111111"
$caTeacherA2Id = [Guid]"11111111-5555-2222-1111-111111111111"
$caTeacherB1Id = [Guid]"22222222-5555-1111-2222-222222222222"

Execute-Sql "INSERT INTO Tenant.CourseAssignments (Id, OrganizationId, BranchId, ClassSectionId, SubjectCourseId, TeacherUserId, AcademicSessionId, IsActive, CreatedBy, CreatedAtUtc, SyncVersion, IsDeleted)
VALUES (@Id, @OrgId, @BranchId, @SectionId, @SubjectId, @TeacherId, @SessionId, 1, 'System', GETUTCDATE(), 1, 0)" @{ Id=$caTeacherA1Id; OrgId=$orgAId; BranchId=$branchA1Id; SectionId=$sec8A1Id; SubjectId=$subjMathAId; TeacherId=$teacherA1Id; SessionId=$sessionAId }

Execute-Sql "INSERT INTO Tenant.CourseAssignments (Id, OrganizationId, BranchId, ClassSectionId, SubjectCourseId, TeacherUserId, AcademicSessionId, IsActive, CreatedBy, CreatedAtUtc, SyncVersion, IsDeleted)
VALUES (@Id, @OrgId, @BranchId, @SectionId, @SubjectId, @TeacherId, @SessionId, 1, 'System', GETUTCDATE(), 1, 0)" @{ Id=$caTeacherA2Id; OrgId=$orgAId; BranchId=$branchA2Id; SectionId=$sec9A1Id; SubjectId=$subjSciAId; TeacherId=$teacherA2Id; SessionId=$sessionAId }

Execute-Sql "INSERT INTO Tenant.CourseAssignments (Id, OrganizationId, BranchId, ClassSectionId, SubjectCourseId, TeacherUserId, AcademicSessionId, IsActive, CreatedBy, CreatedAtUtc, SyncVersion, IsDeleted)
VALUES (@Id, @OrgId, @BranchId, @SectionId, @SubjectId, @TeacherId, @SessionId, 1, 'System', GETUTCDATE(), 1, 0)" @{ Id=$caTeacherB1Id; OrgId=$orgBId; BranchId=$branchB1Id; SectionId=$sec8B1Id; SubjectId=$subjMathBId; TeacherId=$teacherB1Id; SessionId=$sessionBId }

# Timetable Slots for Teacher A1
Execute-Sql "INSERT INTO Tenant.TimetableSlots (Id, OrganizationId, BranchId, AcademicSessionId, ClassGradeId, ClassSectionId, SubjectCourseId, TeacherStaffId, DayOfWeek, StartTime, EndTime, RoomNumber, IsActive, CreatedBy, CreatedAtUtc, SyncVersion, IsDeleted)
VALUES (NEWID(), @OrgId, @BranchId, @SessionId, @GradeId, @SectionId, @SubjectId, @StaffId, 1, '09:00:00', '09:45:00', 'Room 101', 1, 'System', GETUTCDATE(), 1, 0)" @{
    OrgId=$orgAId; BranchId=$branchA1Id; SessionId=$sessionAId; GradeId=$grade8AId; SectionId=$sec8A1Id; SubjectId=$subjMathAId; StaffId=$staffA1ProfileId
}

# Timetable Slots for Teacher A2
Execute-Sql "INSERT INTO Tenant.TimetableSlots (Id, OrganizationId, BranchId, AcademicSessionId, ClassGradeId, ClassSectionId, SubjectCourseId, TeacherStaffId, DayOfWeek, StartTime, EndTime, RoomNumber, IsActive, CreatedBy, CreatedAtUtc, SyncVersion, IsDeleted)
VALUES (NEWID(), @OrgId, @BranchId, @SessionId, @GradeId, @SectionId, @SubjectId, @StaffId, 1, '10:00:00', '10:45:00', 'Room 201', 1, 'System', GETUTCDATE(), 1, 0)" @{
    OrgId=$orgAId; BranchId=$branchA2Id; SessionId=$sessionAId; GradeId=$grade9AId; SectionId=$sec9A1Id; SubjectId=$subjSciAId; StaffId=$staffA2ProfileId
}

# Announcements
Execute-Sql "INSERT INTO Tenant.Announcements (Id, OrganizationId, BranchId, Title, Content, TargetAudience, Priority, PublishDate, IsActive, CreatedBy, CreatedAtUtc, SyncVersion, IsDeleted)
VALUES (NEWID(), @OrgId, @BranchId, 'Welcome to Academic Year 2026', 'Welcome back teachers and students!', 'All', 'Normal', GETUTCDATE(), 1, 'System', GETUTCDATE(), 1, 0)" @{ OrgId=$orgAId; BranchId=$branchA1Id }

Execute-Sql "INSERT INTO Tenant.Announcements (Id, OrganizationId, BranchId, Title, Content, TargetAudience, Priority, PublishDate, IsActive, CreatedBy, CreatedAtUtc, SyncVersion, IsDeleted)
VALUES (NEWID(), @OrgId, @BranchId, 'Admin Board Meeting Notice', 'Confidential board meeting agenda for leadership.', 'Staff', 'Urgent', GETUTCDATE(), 1, 'System', GETUTCDATE(), 1, 0)" @{ OrgId=$orgAId; BranchId=$branchA1Id }

# Fee Invoices
$feeA1Id = [Guid]"11111111-4444-1111-1111-111111111111"
$feeA2Id = [Guid]"11111111-4444-2222-1111-111111111111"

Execute-Sql "INSERT INTO Tenant.FeeInvoices (Id, OrganizationId, BranchId, StudentId, InvoiceNumber, Title, IssueDate, DueDate, TotalAmount, PaidAmount, Status, CreatedBy, CreatedAtUtc, SyncVersion, IsDeleted)
VALUES (@Id, @OrgId, @BranchId, @StudentId, 'INV-2026-001', 'Tuition Fee - October 2026', GETUTCDATE(), DATEADD(day, 15, GETUTCDATE()), 15000.00, 5000.00, 2, 'System', GETUTCDATE(), 1, 0)" @{ Id=$feeA1Id; OrgId=$orgAId; BranchId=$branchA1Id; StudentId=$studRecordA1Id }

Execute-Sql "INSERT INTO Tenant.FeeInvoices (Id, OrganizationId, BranchId, StudentId, InvoiceNumber, Title, IssueDate, DueDate, TotalAmount, PaidAmount, Status, CreatedBy, CreatedAtUtc, SyncVersion, IsDeleted)
VALUES (@Id, @OrgId, @BranchId, @StudentId, 'INV-2026-002', 'Tuition Fee - October 2026', GETUTCDATE(), DATEADD(day, 15, GETUTCDATE()), 18000.00, 0.00, 1, 'System', GETUTCDATE(), 1, 0)" @{ Id=$feeA2Id; OrgId=$orgAId; BranchId=$branchA1Id; StudentId=$studRecordA2Id }

# Student Document for Student A1
$docA1Id = [Guid]"11111111-3333-1111-1111-111111111111"
Execute-Sql "INSERT INTO Tenant.StudentDocuments (Id, OrganizationId, BranchId, StudentId, Title, DocumentType, FileUrl, FileName, FileSizeBytes, Description, CreatedBy, CreatedAtUtc, SyncVersion, IsDeleted)
VALUES (@Id, @OrgId, @BranchId, @StudentId, 'Student Identity Card 2026', 1, '/uploads/docs/idcard-a1.pdf', 'idcard-a1.pdf', 102400, 'Official Student ID Card', 'System', GETUTCDATE(), 1, 0)" @{ Id=$docA1Id; OrgId=$orgAId; BranchId=$branchA1Id; StudentId=$studRecordA1Id }

$conn.Close()
Write-Host "Multi-organization test data successfully initialized with 0 errors!" -ForegroundColor Green
