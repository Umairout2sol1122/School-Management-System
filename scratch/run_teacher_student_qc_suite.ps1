$ErrorActionPreference = "Stop"
$apiUrl = "http://localhost:5080"

Write-Host "================================================================================" -ForegroundColor Cyan
Write-Host "   TEACHER & STUDENT MODULES ENHANCEMENTS - COMPREHENSIVE E2E QC TEST SUITE   " -ForegroundColor Cyan
Write-Host "================================================================================" -ForegroundColor Cyan

$results = [ordered]@{
    TotalScenarios = 0
    Passed = 0
    Failed = 0
    Bugs = @()
    SecurityChecks = [ordered]@{}
}

function Test-Scenario($id, $title, [scriptblock]$action) {
    $results.TotalScenarios++
    Write-Host "`n[$($results.TotalScenarios)] Testing: $id - $title..." -ForegroundColor Yellow
    try {
        & $action
        $results.Passed++
        Write-Host "    --> PASS: $title" -ForegroundColor Green
        return $true
    } catch {
        $results.Failed++
        $errMsg = $_.Exception.Message
        if ($_.Exception.Response) {
            try {
                $stream = $_.Exception.Response.GetResponseStream()
                $reader = New-Object System.IO.StreamReader($stream)
                $body = $reader.ReadToEnd()
                $errMsg += " | Response Body: $body"
            } catch {}
        }
        Write-Host "    --> FAIL: $title - $errMsg" -ForegroundColor Red
        $results.Bugs += @{
            Scenario = "$($id): $title"
            Error = $errMsg
        }
        return $false
    }
}

# -----------------------------------------------------------------------------
# PHASE 0: SETUP & AUTHENTICATION OF ROLES
# -----------------------------------------------------------------------------
Write-Host "`n>>> [PHASE 0] Initializing Multi-Tenant Environment & Roles..." -ForegroundColor Magenta

# 1. SuperAdmin Login
$superAdminToken = ""
Test-Scenario "AUTH-01" "SuperAdmin Login" {
    $payload = @{ emailOrUsername = "superadmin"; password = "SuperAdmin@2026!" } | ConvertTo-Json
    $res = Invoke-RestMethod -Uri "$apiUrl/api/auth/login" -Method Post -Body $payload -ContentType "application/json"
    if (-not $res.data.accessToken) { throw "Missing accessToken in login response." }
    $script:superAdminToken = $res.data.accessToken
}
$saHeaders = @{ "Authorization" = "Bearer $superAdminToken" }

# 2. Setup Organizations (Org A and Org B)
$orgAId = ""
$orgBId = ""
$orgAdminAEmail = "orgadmin.a." + (Get-Random -Minimum 1000 -Maximum 9999) + "@test.com"
$orgAdminBEmail = "orgadmin.b." + (Get-Random -Minimum 1000 -Maximum 9999) + "@test.com"
$defaultPwd = "Password@123"

Test-Scenario "TENANT-01" "Provision Organization A (Apex Academy)" {
    $code = "ORGA-" + (Get-Random -Minimum 1000 -Maximum 9999)
    $payload = @{
        name = "Apex International Academy"
        code = $code
        email = $orgAdminAEmail
        phone = "+92 300 1111111"
        address = "Sector H-12, Islamabad"
        themePrimaryColor = "#924EC0"
        themeSecondaryColor = "#5840BA"
        initialAdminEmail = $orgAdminAEmail
        initialAdminUsername = "admin_" + $code.ToLower()
        initialAdminPassword = $defaultPwd
        initialAdminFirstName = "Apex"
        initialAdminLastName = "Administrator"
    } | ConvertTo-Json
    $res = Invoke-RestMethod -Uri "$apiUrl/api/organizations" -Method Post -Headers $saHeaders -Body $payload -ContentType "application/json"
    $script:orgAId = $res.data.organizationId
    if (-not $script:orgAId) { throw "Organization A creation failed." }
}

Test-Scenario "TENANT-02" "Provision Organization B (Beacon Institute)" {
    $code = "ORGB-" + (Get-Random -Minimum 1000 -Maximum 9999)
    $payload = @{
        name = "Beacon Heights Institute"
        code = $code
        email = $orgAdminBEmail
        phone = "+92 300 2222222"
        address = "Gulberg, Lahore"
        themePrimaryColor = "#1E88E5"
        themeSecondaryColor = "#0D47A1"
        initialAdminEmail = $orgAdminBEmail
        initialAdminUsername = "admin_" + $code.ToLower()
        initialAdminPassword = $defaultPwd
        initialAdminFirstName = "Beacon"
        initialAdminLastName = "Administrator"
    } | ConvertTo-Json
    $res = Invoke-RestMethod -Uri "$apiUrl/api/organizations" -Method Post -Headers $saHeaders -Body $payload -ContentType "application/json"
    $script:orgBId = $res.data.organizationId
    if (-not $script:orgBId) { throw "Organization B creation failed." }
}

# 3. Logins for Org Admins
$orgAdminAToken = ""
$orgAdminBToken = ""

Test-Scenario "AUTH-02" "OrgAdmin A Login" {
    $payload = @{ emailOrUsername = $orgAdminAEmail; password = $defaultPwd } | ConvertTo-Json
    $res = Invoke-RestMethod -Uri "$apiUrl/api/auth/login" -Method Post -Body $payload -ContentType "application/json"
    $script:orgAdminAToken = $res.data.accessToken
    if (-not $script:orgAdminAToken) { throw "OrgAdmin A login failed." }
}
$orgAHeaders = @{ "Authorization" = "Bearer $orgAdminAToken" }

Test-Scenario "AUTH-03" "OrgAdmin B Login" {
    $payload = @{ emailOrUsername = $orgAdminBEmail; password = $defaultPwd } | ConvertTo-Json
    $res = Invoke-RestMethod -Uri "$apiUrl/api/auth/login" -Method Post -Body $payload -ContentType "application/json"
    $script:orgAdminBToken = $res.data.accessToken
    if (-not $script:orgAdminBToken) { throw "OrgAdmin B login failed." }
}
$orgBHeaders = @{ "Authorization" = "Bearer $orgAdminBToken" }

# 4. Provision Branches in Org A (Main Campus A1 and North Campus A2)
$branchA1Id = ""
$branchA2Id = ""

Test-Scenario "BRANCH-01" "Create Branch A1 (Apex Main Campus) in Org A" {
    $payload = @{
        name = "Apex Main Campus"
        code = "BR-A1"
        address = "100 Main St"
        city = "Islamabad"
        phone = "+92 51 1111111"
        email = "main@apex.test"
    } | ConvertTo-Json
    $res = Invoke-RestMethod -Uri "$apiUrl/api/branches" -Method Post -Headers $orgAHeaders -Body $payload -ContentType "application/json"
    $script:branchA1Id = $res.data.id
    if (-not $script:branchA1Id) { throw "Branch A1 creation failed." }
}

Test-Scenario "BRANCH-02" "Create Branch A2 (Apex North Campus) in Org A" {
    $payload = @{
        name = "Apex North Campus"
        code = "BR-A2"
        address = "200 North Rd"
        city = "Islamabad"
        phone = "+92 51 2222222"
        email = "north@apex.test"
    } | ConvertTo-Json
    $res = Invoke-RestMethod -Uri "$apiUrl/api/branches" -Method Post -Headers $orgAHeaders -Body $payload -ContentType "application/json"
    $script:branchA2Id = $res.data.id
    if (-not $script:branchA2Id) { throw "Branch A2 creation failed." }
}

# 5. Provision Branch in Org B (City Campus B1)
$branchB1Id = ""
Test-Scenario "BRANCH-03" "Create Branch B1 in Org B" {
    $payload = @{
        name = "Beacon City Campus"
        code = "BR-B1"
        address = "50 Mall Rd"
        city = "Lahore"
        phone = "+92 42 1111111"
        email = "city@beacon.test"
    } | ConvertTo-Json
    $res = Invoke-RestMethod -Uri "$apiUrl/api/branches" -Method Post -Headers $orgBHeaders -Body $payload -ContentType "application/json"
    $script:branchB1Id = $res.data.id
    if (-not $script:branchB1Id) { throw "Branch B1 creation failed." }
}

# 6. Provision Academic Entities in Org A
$grade8AId = ""
$grade9AId = ""
$sec8A1Id  = ""
$sec8A2Id  = ""
$sec9A1Id  = ""
$subjMathAId = ""
$subjSciAId  = ""

Test-Scenario "ACAD-01" "Create Academic Setup in Org A (Grades, Sections, Subjects)" {
    # Grade 8 in Branch A1
    $p = @{ name = "Grade 8"; code = "G8-A"; branchId = $branchA1Id; displayOrder = 8 } | ConvertTo-Json
    $r = Invoke-RestMethod -Uri "$apiUrl/api/academics/grades" -Method Post -Headers $orgAHeaders -Body $p -ContentType "application/json"
    $script:grade8AId = $r.data.id

    # Grade 9 in Branch A2
    $p = @{ name = "Grade 9"; code = "G9-A"; branchId = $branchA2Id; displayOrder = 9 } | ConvertTo-Json
    $r = Invoke-RestMethod -Uri "$apiUrl/api/academics/grades" -Method Post -Headers $orgAHeaders -Body $p -ContentType "application/json"
    $script:grade9AId = $r.data.id

    # Section A under Grade 8
    $p = @{ name = "Section A"; classGradeId = $script:grade8AId; branchId = $branchA1Id; roomNumber = "Room 101"; capacity = 30 } | ConvertTo-Json
    $r = Invoke-RestMethod -Uri "$apiUrl/api/academics/sections" -Method Post -Headers $orgAHeaders -Body $p -ContentType "application/json"
    $script:sec8A1Id = $r.data.id

    # Section B under Grade 8
    $p = @{ name = "Section B"; classGradeId = $script:grade8AId; branchId = $branchA1Id; roomNumber = "Room 102"; capacity = 30 } | ConvertTo-Json
    $r = Invoke-RestMethod -Uri "$apiUrl/api/academics/sections" -Method Post -Headers $orgAHeaders -Body $p -ContentType "application/json"
    $script:sec8A2Id = $r.data.id

    # Section A under Grade 9 (Branch A2)
    $p = @{ name = "Section A"; classGradeId = $script:grade9AId; branchId = $branchA2Id; roomNumber = "Room 201"; capacity = 30 } | ConvertTo-Json
    $r = Invoke-RestMethod -Uri "$apiUrl/api/academics/sections" -Method Post -Headers $orgAHeaders -Body $p -ContentType "application/json"
    $script:sec9A1Id = $r.data.id

    # Subject Mathematics in Branch A1
    $p = @{ name = "Mathematics"; code = "MATH-8"; classGradeId = $script:grade8AId; branchId = $branchA1Id; creditHours = 3 } | ConvertTo-Json
    $r = Invoke-RestMethod -Uri "$apiUrl/api/academics/subjects" -Method Post -Headers $orgAHeaders -Body $p -ContentType "application/json"
    $script:subjMathAId = $r.data.id

    # Subject Science in Branch A2
    $p = @{ name = "Science"; code = "SCI-9"; classGradeId = $script:grade9AId; branchId = $branchA2Id; creditHours = 3 } | ConvertTo-Json
    $r = Invoke-RestMethod -Uri "$apiUrl/api/academics/subjects" -Method Post -Headers $orgAHeaders -Body $p -ContentType "application/json"
    $script:subjSciAId = $r.data.id
}

# 7. Provision Academic Entities in Org B
$grade8BId = ""
$sec8B1Id  = ""
$subjMathBId = ""

Test-Scenario "ACAD-02" "Create Academic Setup in Org B (Grade 8, Section A, Math)" {
    $p = @{ name = "Grade 8"; code = "G8-B"; branchId = $branchB1Id; displayOrder = 8 } | ConvertTo-Json
    $r = Invoke-RestMethod -Uri "$apiUrl/api/academics/grades" -Method Post -Headers $orgBHeaders -Body $p -ContentType "application/json"
    $script:grade8BId = $r.data.id

    $p = @{ name = "Section A"; classGradeId = $script:grade8BId; branchId = $branchB1Id; roomNumber = "Room 101"; capacity = 30 } | ConvertTo-Json
    $r = Invoke-RestMethod -Uri "$apiUrl/api/academics/sections" -Method Post -Headers $orgBHeaders -Body $p -ContentType "application/json"
    $script:sec8B1Id = $r.data.id

    $p = @{ name = "Mathematics"; code = "MATH-8B"; classGradeId = $script:grade8BId; branchId = $branchB1Id; creditHours = 3 } | ConvertTo-Json
    $r = Invoke-RestMethod -Uri "$apiUrl/api/academics/subjects" -Method Post -Headers $orgBHeaders -Body $p -ContentType "application/json"
    $script:subjMathBId = $r.data.id
}

# 8. Onboard Teachers
$teacherA1Email = "teacherA1." + (Get-Random -Minimum 1000 -Maximum 9999) + "@apex.test"
$teacherA2Email = "teacherA2." + (Get-Random -Minimum 1000 -Maximum 9999) + "@apex.test"
$teacherB1Email = "teacherB1." + (Get-Random -Minimum 1000 -Maximum 9999) + "@beacon.test"

$teacherA1UserId = ""
$teacherA2UserId = ""
$teacherB1UserId = ""

Test-Scenario "STAFF-01" "Onboard Teacher A1 in Org A (Branch A1, Math Teacher)" {
    $payload = @{
        branchId = $branchA1Id
        firstName = "Tariq"
        lastName = "Mahmood"
        email = $teacherA1Email
        phoneNumber = "+92 300 3333333"
        designation = "Senior Mathematics Teacher"
        department = "Academics"
        qualification = "M.Sc Mathematics"
        specialization = "Algebra"
        employmentType = 1 # FullTime
        initialPassword = $defaultPwd
    } | ConvertTo-Json
    $res = Invoke-RestMethod -Uri "$apiUrl/api/staff" -Method Post -Headers $orgAHeaders -Body $payload -ContentType "application/json"
    $script:teacherA1UserId = $res.data.userId
    if (-not $script:teacherA1UserId) { throw "Teacher A1 onboarding failed." }
}

Test-Scenario "STAFF-02" "Onboard Teacher A2 in Org A (Branch A2, Science Teacher)" {
    $payload = @{
        branchId = $branchA2Id
        firstName = "Sadia"
        lastName = "Khan"
        email = $teacherA2Email
        phoneNumber = "+92 300 4444444"
        designation = "Science Teacher"
        department = "Science"
        qualification = "M.Sc Physics"
        specialization = "Physics"
        employmentType = 1 # FullTime
        initialPassword = $defaultPwd
    } | ConvertTo-Json
    $res = Invoke-RestMethod -Uri "$apiUrl/api/staff" -Method Post -Headers $orgAHeaders -Body $payload -ContentType "application/json"
    $script:teacherA2UserId = $res.data.userId
    if (-not $script:teacherA2UserId) { throw "Teacher A2 onboarding failed." }
}

Test-Scenario "STAFF-03" "Onboard Teacher B1 in Org B (Branch B1, Math Teacher)" {
    $payload = @{
        branchId = $branchB1Id
        firstName = "Usman"
        lastName = "Qureshi"
        email = $teacherB1Email
        phoneNumber = "+92 300 5555555"
        designation = "Mathematics Faculty"
        department = "Academics"
        qualification = "M.Sc Mathematics"
        specialization = "Applied Math"
        employmentType = 1 # FullTime
        initialPassword = $defaultPwd
    } | ConvertTo-Json
    $res = Invoke-RestMethod -Uri "$apiUrl/api/staff" -Method Post -Headers $orgBHeaders -Body $payload -ContentType "application/json"
    $script:teacherB1UserId = $res.data.userId
    if (-not $script:teacherB1UserId) { throw "Teacher B1 onboarding failed." }
}

# 9. Admit Students & Create Portal Access
$studentA1Email = "studentA1." + (Get-Random -Minimum 1000 -Maximum 9999) + "@apex.test"
$studentA2Email = "studentA2." + (Get-Random -Minimum 1000 -Maximum 9999) + "@apex.test"
$studentB1Email = "studentB1." + (Get-Random -Minimum 1000 -Maximum 9999) + "@beacon.test"

$studentA1RecordId = ""
$studentA2RecordId = ""
$studentB1RecordId = ""

Test-Scenario "STUD-01" "Admit Student A1 (Ali Raza - Grade 8 Section A) in Org A" {
    $payload = @{
        branchId = $branchA1Id
        firstName = "Ali"
        lastName = "Raza"
        currentGrade = "Grade 8"
        section = "Section A"
        rollNumber = "8A-01"
        gender = 1 # Male
        dateOfBirth = "2012-05-14"
        address = "12 Main St"
        city = "Islamabad"
        emergencyContactName = "Raza Ahmed"
        emergencyContactPhone = "+92 300 7777777"
    } | ConvertTo-Json
    $res = Invoke-RestMethod -Uri "$apiUrl/api/students" -Method Post -Headers $orgAHeaders -Body $payload -ContentType "application/json"
    $script:studentA1RecordId = $res.data.id

    # Create portal access
    $accessPayload = @{ email = $studentA1Email; password = $defaultPwd } | ConvertTo-Json
    $accRes = Invoke-RestMethod -Uri "$apiUrl/api/students/$($script:studentA1RecordId)/portal-access" -Method Post -Headers $orgAHeaders -Body $accessPayload -ContentType "application/json"
    if (-not $accRes.data.userId) { throw "Student A1 portal access creation failed." }
}

Test-Scenario "STUD-02" "Admit Student A2 (Bilal Ahmed - Grade 8 Section B) in Org A" {
    $payload = @{
        branchId = $branchA1Id
        firstName = "Bilal"
        lastName = "Ahmed"
        currentGrade = "Grade 8"
        section = "Section B"
        rollNumber = "8B-01"
        gender = 1 # Male
        dateOfBirth = "2012-09-20"
        address = "34 North St"
        city = "Islamabad"
    } | ConvertTo-Json
    $res = Invoke-RestMethod -Uri "$apiUrl/api/students" -Method Post -Headers $orgAHeaders -Body $payload -ContentType "application/json"
    $script:studentA2RecordId = $res.data.id

    $accessPayload = @{ email = $studentA2Email; password = $defaultPwd } | ConvertTo-Json
    $accRes = Invoke-RestMethod -Uri "$apiUrl/api/students/$($script:studentA2RecordId)/portal-access" -Method Post -Headers $orgAHeaders -Body $accessPayload -ContentType "application/json"
    if (-not $accRes.data.userId) { throw "Student A2 portal access creation failed." }
}

Test-Scenario "STUD-03" "Admit Student B1 (Fatima Noor - Grade 8 Section A) in Org B" {
    $payload = @{
        branchId = $branchB1Id
        firstName = "Fatima"
        lastName = "Noor"
        currentGrade = "Grade 8"
        section = "Section A"
        rollNumber = "8A-01"
        gender = 2 # Female
        dateOfBirth = "2012-03-10"
        address = "56 Mall Rd"
        city = "Lahore"
    } | ConvertTo-Json
    $res = Invoke-RestMethod -Uri "$apiUrl/api/students" -Method Post -Headers $orgBHeaders -Body $payload -ContentType "application/json"
    $script:studentB1RecordId = $res.data.id

    $accessPayload = @{ email = $studentB1Email; password = $defaultPwd } | ConvertTo-Json
    $accRes = Invoke-RestMethod -Uri "$apiUrl/api/students/$($script:studentB1RecordId)/portal-access" -Method Post -Headers $orgBHeaders -Body $accessPayload -ContentType "application/json"
    if (-not $accRes.data.userId) { throw "Student B1 portal access creation failed." }
}

# 10. Dynamic Course Assignments
Test-Scenario "ASSIGN-01" "Assign Teacher A1 to Grade 8 Section A Mathematics in Org A" {
    $payload = @{
        branchId = $branchA1Id
        classSectionId = $sec8A1Id
        subjectCourseId = $subjMathAId
        teacherUserId = $teacherA1UserId
    } | ConvertTo-Json
    $res = Invoke-RestMethod -Uri "$apiUrl/api/academics/assignments" -Method Post -Headers $orgAHeaders -Body $payload -ContentType "application/json"
    if (-not $res.data.id) { throw "CourseAssignment for Teacher A1 failed." }
}

Test-Scenario "ASSIGN-02" "Assign Teacher A2 to Grade 9 Section A Science in Org A" {
    $payload = @{
        branchId = $branchA2Id
        classSectionId = $sec9A1Id
        subjectCourseId = $subjSciAId
        teacherUserId = $teacherA2UserId
    } | ConvertTo-Json
    $res = Invoke-RestMethod -Uri "$apiUrl/api/academics/assignments" -Method Post -Headers $orgAHeaders -Body $payload -ContentType "application/json"
    if (-not $res.data.id) { throw "CourseAssignment for Teacher A2 failed." }
}

Test-Scenario "ASSIGN-03" "Assign Teacher B1 to Grade 8 Section A Mathematics in Org B" {
    $payload = @{
        branchId = $branchB1Id
        classSectionId = $sec8B1Id
        subjectCourseId = $subjMathBId
        teacherUserId = $teacherB1UserId
    } | ConvertTo-Json
    $res = Invoke-RestMethod -Uri "$apiUrl/api/academics/assignments" -Method Post -Headers $orgBHeaders -Body $payload -ContentType "application/json"
    if (-not $res.data.id) { throw "CourseAssignment for Teacher B1 failed." }
}

# -----------------------------------------------------------------------------
# PHASE 1: TEACHER PORTAL E2E & SECURITY TESTING
# -----------------------------------------------------------------------------
Write-Host "`n>>> [PHASE 1] Executing Teacher Portal E2E & Security Workflows..." -ForegroundColor Magenta

# 1. Teacher A1 Authentication
$teacherA1Token = ""
Test-Scenario "TEACH-AUTH-01" "Teacher A1 Login and System Role Auto-Healing" {
    $payload = @{ emailOrUsername = $teacherA1Email; password = $defaultPwd } | ConvertTo-Json
    $res = Invoke-RestMethod -Uri "$apiUrl/api/auth/login" -Method Post -Body $payload -ContentType "application/json"
    $script:teacherA1Token = $res.data.accessToken
    if (-not $script:teacherA1Token) { throw "Teacher A1 login failed." }
}
$teacherA1Headers = @{ "Authorization" = "Bearer $teacherA1Token" }

# Teacher A2 Authentication
$teacherA2Token = ""
Test-Scenario "TEACH-AUTH-02" "Teacher A2 Login" {
    $payload = @{ emailOrUsername = $teacherA2Email; password = $defaultPwd } | ConvertTo-Json
    $res = Invoke-RestMethod -Uri "$apiUrl/api/auth/login" -Method Post -Body $payload -ContentType "application/json"
    $script:teacherA2Token = $res.data.accessToken
}
$teacherA2Headers = @{ "Authorization" = "Bearer $teacherA2Token" }

# 2. Teacher Profile
Test-Scenario "TEACH-PROF-01" "Teacher A1 Profile Retrieval & Scoped Metrics" {
    $res = Invoke-RestMethod -Uri "$apiUrl/api/teacher/profile" -Method Get -Headers $teacherA1Headers
    $prof = $res.data
    if ($prof.employeeCode -notmatch "EMP") { throw "Invalid employee code: $($prof.employeeCode)" }
    if ($prof.assignedClassesCount -lt 1) { throw "Expected assignedClassesCount >= 1, got $($prof.assignedClassesCount)" }
    if ($prof.assignedSubjectsCount -lt 1) { throw "Expected assignedSubjectsCount >= 1, got $($prof.assignedSubjectsCount)" }
}

Test-Scenario "TEACH-PROF-02" "Teacher A1 Contact Details Update" {
    $payload = @{
        phoneNumber = "+92 300 9988776"
        emergencyContactName = "Aslam Mahmood"
        emergencyContactPhone = "+92 300 1122334"
    } | ConvertTo-Json
    $res = Invoke-RestMethod -Uri "$apiUrl/api/teacher/profile" -Method Put -Headers $teacherA1Headers -Body $payload -ContentType "application/json"
    if ($res.success -ne $true) { throw "Teacher profile update failed." }
}

# 3. Teacher Dashboard
Test-Scenario "TEACH-DASH-01" "Teacher A1 Dashboard Scoped Data" {
    $res = Invoke-RestMethod -Uri "$apiUrl/api/teacher/dashboard" -Method Get -Headers $teacherA1Headers
    $dash = $res.data
    if ($dash.teacherName -notmatch "Tariq") { throw "Dashboard teacherName mismatch: $($dash.teacherName)" }
    if ($dash.assignedClasses.Count -ne 1) { throw "Expected exactly 1 assigned class, got $($dash.assignedClasses.Count)" }
    if ($dash.assignedClasses[0].gradeName -ne "Grade 8") { throw "Assigned class mismatch: $($dash.assignedClasses[0].gradeName)" }
}

# 4. Teacher Classes & Dynamic Course Assignment Isolation
Test-Scenario "TEACH-CLASS-01" "Teacher A1 Classes View (Shows Assigned Grade 8 Section A only)" {
    $res = Invoke-RestMethod -Uri "$apiUrl/api/teacher/classes" -Method Get -Headers $teacherA1Headers
    $classes = $res.data
    if ($classes.Count -ne 1) { throw "Expected 1 assigned class, found $($classes.Count)" }
    if ($classes[0].classGradeName -ne "Grade 8") { throw "Class name mismatch: $($classes[0].classGradeName)" }
}

Test-Scenario "TEACH-CLASS-02" "Teacher A1 View Students in Assigned Class" {
    $res = Invoke-RestMethod -Uri "$apiUrl/api/teacher/classes/$grade8AId/students?classSectionId=$sec8A1Id" -Method Get -Headers $teacherA1Headers
    $students = $res.data
    if ($students.Count -lt 1) { throw "Expected at least 1 student in assigned section, found $($students.Count)" }
    if ($students[0].fullName -notmatch "Ali Raza") { throw "Student name mismatch: $($students[0].fullName)" }
}

Test-Scenario "SEC-TEACH-01" "SECURITY: Teacher A1 Direct Request to Unassigned Grade 9 Students (Must Fail with 403)" {
    $failed = $false
    try {
        Invoke-RestMethod -Uri "$apiUrl/api/teacher/classes/$grade9AId/students" -Method Get -Headers $teacherA1Headers -ErrorAction Stop
    } catch {
        if ($_.Exception.Response.StatusCode.value__ -in @(403, 404)) {
            $failed = $true
        }
    }
    if (-not $failed) { throw "SECURITY LOOPHOLE: Teacher accessed unassigned class students!" }
    $results.SecurityChecks["TeacherClassIsolation"] = "PASS"
}

# 5. Teacher Attendance Operations
Test-Scenario "TEACH-ATT-01" "Teacher A1 Retrieve Attendance Sheet" {
    $today = (Get-Date).ToString("yyyy-MM-dd")
    $res = Invoke-RestMethod -Uri "$apiUrl/api/teacher/attendance/sheet?classGradeId=$grade8AId&classSectionId=$sec8A1Id&date=$today" -Method Get -Headers $teacherA1Headers
    if ($res.data.students.Count -lt 1) { throw "Attendance sheet returned 0 students." }
}

Test-Scenario "TEACH-ATT-02" "Teacher A1 Mark Daily Attendance for Assigned Class" {
    $today = (Get-Date).ToString("yyyy-MM-dd")
    $payload = @{
        classGradeId = $grade8AId
        classSectionId = $sec8A1Id
        subjectCourseId = $subjMathAId
        date = $today
        entries = @(
            @{
                studentId = $studentA1RecordId
                status = 1 # Present
                remarks = "Present and attentive"
            }
        )
    } | ConvertTo-Json
    $res = Invoke-RestMethod -Uri "$apiUrl/api/teacher/attendance" -Method Post -Headers $teacherA1Headers -Body $payload -ContentType "application/json"
    if ($res.success -ne $true) { throw "Attendance marking failed." }
}

Test-Scenario "TEACH-ATT-03" "Duplicate Attendance Submission Block (Returns 400)" {
    $today = (Get-Date).ToString("yyyy-MM-dd")
    $payload = @{
        classGradeId = $grade8AId
        classSectionId = $sec8A1Id
        subjectCourseId = $subjMathAId
        date = $today
        entries = @(
            @{ studentId = $studentA1RecordId; status = 1 }
        )
    } | ConvertTo-Json
    $blocked = $false
    try {
        Invoke-RestMethod -Uri "$apiUrl/api/teacher/attendance" -Method Post -Headers $teacherA1Headers -Body $payload -ContentType "application/json" -ErrorAction Stop
    } catch {
        if ($_.Exception.Response.StatusCode.value__ -eq 400) { $blocked = $true }
    }
    if (-not $blocked) { throw "Duplicate attendance was accepted without validation!" }
}

Test-Scenario "SEC-TEACH-02" "SECURITY: Teacher A1 Attempt to Mark Attendance for Unassigned Grade 9 (Must Fail with 403)" {
    $today = (Get-Date).ToString("yyyy-MM-dd")
    $payload = @{
        classGradeId = $grade9AId
        classSectionId = $sec9A1Id
        subjectCourseId = $subjSciAId
        date = $today
        entries = @(
            @{ studentId = $studentA1RecordId; status = 1 }
        )
    } | ConvertTo-Json
    $blocked = $false
    try {
        Invoke-RestMethod -Uri "$apiUrl/api/teacher/attendance" -Method Post -Headers $teacherA1Headers -Body $payload -ContentType "application/json" -ErrorAction Stop
    } catch {
        if ($_.Exception.Response.StatusCode.value__ -in @(403, 404)) { $blocked = $true }
    }
    if (-not $blocked) { throw "SECURITY LOOPHOLE: Teacher marked attendance for unassigned class!" }
    $results.SecurityChecks["TeacherAttendanceIsolation"] = "PASS"
}

# 6. Teacher Timetable Slots
Test-Scenario "TEACH-TIME-01" "Create Timetable Slot for Teacher A1 and Verify Scoped View" {
    # Create slot via Admin
    $payload = @{
        branchId = $branchA1Id
        classGradeId = $grade8AId
        classSectionId = $sec8A1Id
        subjectCourseId = $subjMathAId
        teacherStaffId = $teacherA1UserId
        dayOfWeek = 1 # Monday
        startTime = "09:00:00"
        endTime = "09:45:00"
        roomNumber = "Room 101"
    } | ConvertTo-Json
    $slotRes = Invoke-RestMethod -Uri "$apiUrl/api/academics/timetable" -Method Post -Headers $orgAHeaders -Body $payload -ContentType "application/json"
    if (-not $slotRes.data.id) { throw "Timetable slot creation failed." }

    # Verify Teacher A1 timetable
    $res = Invoke-RestMethod -Uri "$apiUrl/api/teacher/timetable" -Method Get -Headers $teacherA1Headers
    if ($res.data.Count -lt 1) { throw "Teacher timetable returned 0 slots." }
    if ($res.data[0].subjectName -ne "Mathematics") { throw "Subject name mismatch in timetable: $($res.data[0].subjectName)" }
}

# 7. Teacher Assignments
$assignmentId = ""
Test-Scenario "TEACH-ASGN-01" "Teacher A1 Create Homework Assignment" {
    $dueDate = (Get-Date).AddDays(5).ToString("yyyy-MM-ddTHH:mm:ssZ")
    $payload = @{
        branchId = $branchA1Id
        classGradeId = $grade8AId
        classSectionId = $sec8A1Id
        subjectCourseId = $subjMathAId
        title = "Algebra Worksheet 1: Quadratic Equations"
        description = "Complete problems 1 to 15 on page 42."
        dueDateUtc = $dueDate
        totalMarks = 50.0
    } | ConvertTo-Json
    $res = Invoke-RestMethod -Uri "$apiUrl/api/teacher/assignments" -Method Post -Headers $teacherA1Headers -Body $payload -ContentType "application/json"
    $script:assignmentId = if ($res.data.id) { $res.data.id } else { $res.data }
    if (-not $script:assignmentId) { throw "Assignment creation failed." }
}

Test-Scenario "SEC-TEACH-03" "SECURITY: Teacher A1 Attempt to Create Assignment for Unassigned Grade 9 (Must Fail with 403)" {
    $dueDate = (Get-Date).AddDays(5).ToString("yyyy-MM-ddTHH:mm:ssZ")
    $payload = @{
        branchId = $branchA2Id
        classGradeId = $grade9AId
        classSectionId = $sec9A1Id
        subjectCourseId = $subjSciAId
        title = "Unauthorized Assignment"
        description = "Should fail."
        dueDateUtc = $dueDate
        totalMarks = 50.0
    } | ConvertTo-Json
    $blocked = $false
    try {
        Invoke-RestMethod -Uri "$apiUrl/api/teacher/assignments" -Method Post -Headers $teacherA1Headers -Body $payload -ContentType "application/json" -ErrorAction Stop
    } catch {
        if ($_.Exception.Response.StatusCode.value__ -in @(403, 404)) { $blocked = $true }
    }
    if (-not $blocked) { throw "SECURITY LOOPHOLE: Teacher created assignment for unassigned class!" }
    $results.SecurityChecks["TeacherAssignmentScope"] = "PASS"
}

# 8. Teacher Exams & Results Entry
$examId = ""
Test-Scenario "TEACH-EXAM-01" "Schedule Exam and Enter Marks for Students" {
    # OrgAdmin schedules exam
    $examDate = (Get-Date).AddDays(10).ToString("yyyy-MM-ddTHH:mm:ssZ")
    $payload = @{
        branchId = $branchA1Id
        classGradeId = $grade8AId
        classSectionId = $sec8A1Id
        subjectCourseId = $subjMathAId
        title = "Mid-Term Examination: Mathematics"
        examDateUtc = $examDate
        durationMinutes = 90
        totalMarks = 100.0
        passingMarks = 40.0
    } | ConvertTo-Json
    $examRes = Invoke-RestMethod -Uri "$apiUrl/api/academics/exams" -Method Post -Headers $orgAHeaders -Body $payload -ContentType "application/json"
    $script:examId = if ($examRes.data.id) { $examRes.data.id } else { $examRes.data }

    # Teacher A1 enters marks for Student A1
    $resPayload = @{
        entries = @(
            @{
                studentId = $studentA1RecordId
                marksObtained = 88.0
                remarks = "Strong algebra skills"
            }
        )
    } | ConvertTo-Json
    $res = Invoke-RestMethod -Uri "$apiUrl/api/teacher/exams/$($script:examId)/results" -Method Post -Headers $teacherA1Headers -Body $resPayload -ContentType "application/json"
    if ($res.success -ne $true) { throw "Exam results entry failed." }
}

Test-Scenario "TEACH-EXAM-02" "Edit Exam Marks Before Final Lock" {
    # Teacher edits marks: 92.0
    $resPayload = @{
        entries = @(
            @{
                studentId = $studentA1RecordId
                marksObtained = 92.0
                remarks = "Bonus mark for extra problem"
            }
        )
    } | ConvertTo-Json
    $res = Invoke-RestMethod -Uri "$apiUrl/api/teacher/exams/$($script:examId)/results" -Method Post -Headers $teacherA1Headers -Body $resPayload -ContentType "application/json"
    if ($res.success -ne $true) { throw "Editing exam marks failed." }
}

Test-Scenario "TEACH-EXAM-03" "Finalize & Lock Exam Results and Prevent Further Tampering" {
    # Lock exam results
    $res = Invoke-RestMethod -Uri "$apiUrl/api/teacher/exams/$($script:examId)/lock" -Method Put -Headers $teacherA1Headers
    if ($res.success -ne $true) { throw "Locking exam results failed." }

    # Attempt to tamper with locked marks
    $tamperPayload = @{
        entries = @(
            @{ studentId = $studentA1RecordId; marksObtained = 50.0 }
        )
    } | ConvertTo-Json
    $blocked = $false
    try {
        Invoke-RestMethod -Uri "$apiUrl/api/teacher/exams/$($script:examId)/results" -Method Post -Headers $teacherA1Headers -Body $tamperPayload -ContentType "application/json" -ErrorAction Stop
    } catch {
        if ($_.Exception.Response.StatusCode.value__ -eq 400) { $blocked = $true }
    }
    if (-not $blocked) { throw "TAMPER BUG: Locked exam marks were modified!" }
    $results.SecurityChecks["ExamLockProtection"] = "PASS"
}

# 9. Teacher Leave Request & Admin Approval
$teacherLeaveId = ""
Test-Scenario "TEACH-LEAVE-01" "Teacher A1 Submit Leave Request" {
    $payload = @{
        leaveType = "Casual"
        startDate = (Get-Date).AddDays(7).ToString("yyyy-MM-dd")
        endDate = (Get-Date).AddDays(8).ToString("yyyy-MM-dd")
        reason = "Attending family function"
    } | ConvertTo-Json
    $res = Invoke-RestMethod -Uri "$apiUrl/api/teacher/leave" -Method Post -Headers $teacherA1Headers -Body $payload -ContentType "application/json"
    $script:teacherLeaveId = if ($res.data.id) { $res.data.id } else { $res.data }
    if (-not $script:teacherLeaveId) { throw "Teacher leave submission failed." }
}

Test-Scenario "SEC-TEACH-04" "SECURITY: Teacher Cannot Self-Approve Leave Request" {
    $payload = @{ status = 2; remarks = "Approved by myself" } | ConvertTo-Json
    $blocked = $false
    try {
        Invoke-RestMethod -Uri "$apiUrl/api/academics/leave/$($script:teacherLeaveId)/decision" -Method Put -Headers $teacherA1Headers -Body $payload -ContentType "application/json" -ErrorAction Stop
    } catch {
        if ($_.Exception.Response.StatusCode.value__ -in @(401, 403)) { $blocked = $true }
    }
    if (-not $blocked) { throw "SECURITY LOOPHOLE: Teacher approved own leave!" }
    $results.SecurityChecks["TeacherSelfLeaveApproval"] = "PASS"
}

Test-Scenario "TEACH-LEAVE-02" "OrgAdmin Decision Approves Teacher Leave" {
    $payload = @{ status = 2; remarks = "Approved by Principal" } | ConvertTo-Json
    $res = Invoke-RestMethod -Uri "$apiUrl/api/academics/leave/$($script:teacherLeaveId)/decision" -Method Put -Headers $orgAHeaders -Body $payload -ContentType "application/json"
    if ($res.success -ne $true) { throw "Leave decision by Admin failed." }

    # Teacher verifies status is Approved
    $leaveList = Invoke-RestMethod -Uri "$apiUrl/api/teacher/leave" -Method Get -Headers $teacherA1Headers
    $item = $leaveList.data | Where-Object { $_.id -eq $script:teacherLeaveId }
    $isApproved = ($item.status -eq "Approved") -or ($item.statusName -eq "Approved") -or ($item.status -eq 2)
    if (-not $isApproved) { throw "Expected status Approved, got $($item.status)" }
}

# 10. Teacher Attendance Report
Test-Scenario "TEACH-REP-01" "Teacher Attendance Report for Assigned Class" {
    $today = (Get-Date).ToString("yyyy-MM-dd")
    $res = Invoke-RestMethod -Uri "$apiUrl/api/teacher/reports/attendance?classGradeId=$grade8AId&startDate=$today&endDate=$today" -Method Get -Headers $teacherA1Headers
    if ($res.data.records.Count -lt 1) { throw "Attendance report returned 0 records." }
}

Test-Scenario "SEC-TEACH-05" "SECURITY: Teacher Access to Unassigned Class Report (Must Fail with 403)" {
    $today = (Get-Date).ToString("yyyy-MM-dd")
    $blocked = $false
    try {
        Invoke-RestMethod -Uri "$apiUrl/api/teacher/reports/attendance?classGradeId=$grade9AId&startDate=$today&endDate=$today" -Method Get -Headers $teacherA1Headers -ErrorAction Stop
    } catch {
        if ($_.Exception.Response.StatusCode.value__ -in @(403, 404)) { $blocked = $true }
    }
    if (-not $blocked) { throw "SECURITY LOOPHOLE: Teacher accessed attendance report for unassigned class!" }
    $results.SecurityChecks["TeacherReportScope"] = "PASS"
}

# -----------------------------------------------------------------------------
# PHASE 2: STUDENT PORTAL E2E & SECURITY TESTING
# -----------------------------------------------------------------------------
Write-Host "`n>>> [PHASE 2] Executing Student Portal E2E & Security Workflows..." -ForegroundColor Magenta

# 1. Student A1 Authentication
$studentA1Token = ""
Test-Scenario "STUD-AUTH-01" "Student A1 Login" {
    $payload = @{ emailOrUsername = $studentA1Email; password = $defaultPwd } | ConvertTo-Json
    $res = Invoke-RestMethod -Uri "$apiUrl/api/auth/login" -Method Post -Body $payload -ContentType "application/json"
    $script:studentA1Token = $res.data.accessToken
    if (-not $script:studentA1Token) { throw "Student A1 login failed." }
}
$studentA1Headers = @{ "Authorization" = "Bearer $studentA1Token" }

# Student A2 Authentication
$studentA2Token = ""
Test-Scenario "STUD-AUTH-02" "Student A2 Login" {
    $payload = @{ emailOrUsername = $studentA2Email; password = $defaultPwd } | ConvertTo-Json
    $res = Invoke-RestMethod -Uri "$apiUrl/api/auth/login" -Method Post -Body $payload -ContentType "application/json"
    $script:studentA2Token = $res.data.accessToken
}
$studentA2Headers = @{ "Authorization" = "Bearer $studentA2Token" }

# Student B1 Authentication
$studentB1Token = ""
Test-Scenario "STUD-AUTH-03" "Student B1 Login (Org B)" {
    $payload = @{ emailOrUsername = $studentB1Email; password = $defaultPwd } | ConvertTo-Json
    $res = Invoke-RestMethod -Uri "$apiUrl/api/auth/login" -Method Post -Body $payload -ContentType "application/json"
    $script:studentB1Token = $res.data.accessToken
}
$studentB1Headers = @{ "Authorization" = "Bearer $studentB1Token" }

# 2. Student Profile & Dashboard
Test-Scenario "STUD-PROF-01" "Student A1 Profile Retrieval (Own Record Only)" {
    $res = Invoke-RestMethod -Uri "$apiUrl/api/student/profile" -Method Get -Headers $studentA1Headers
    $prof = $res.data
    if ($prof.fullName -notmatch "Ali Raza") { throw "Student profile fullName mismatch: $($prof.fullName)" }
    if ($prof.currentGrade -ne "Grade 8") { throw "Grade mismatch: $($prof.currentGrade)" }
    if ($prof.section -ne "Section A") { throw "Section mismatch: $($prof.section)" }
}

Test-Scenario "STUD-PROF-02" "Student A1 Contact Details Update" {
    $payload = @{
        emergencyContactPhone = "+92 300 9991111"
        address = "12 Main St Updated"
        city = "Islamabad"
    } | ConvertTo-Json
    $res = Invoke-RestMethod -Uri "$apiUrl/api/student/profile" -Method Put -Headers $studentA1Headers -Body $payload -ContentType "application/json"
    if ($res.success -ne $true) { throw "Student profile contact update failed." }
}

Test-Scenario "STUD-DASH-01" "Student A1 Dashboard Scoped Data" {
    $res = Invoke-RestMethod -Uri "$apiUrl/api/student/dashboard" -Method Get -Headers $studentA1Headers
    $dash = $res.data
    $actualGrade = if ($dash.currentGrade) { $dash.currentGrade } else { $dash.grade }
    if ($actualGrade -ne "Grade 8") { throw "Dashboard grade mismatch: $actualGrade" }
}

# 3. Student Attendance
Test-Scenario "STUD-ATT-01" "Student A1 View Attendance History & Percentage" {
    $res = Invoke-RestMethod -Uri "$apiUrl/api/student/attendance" -Method Get -Headers $studentA1Headers
    $att = $res.data
    if ($att.totalDays -lt 1) { throw "Expected totalDays >= 1, got $($att.totalDays)" }
    if ($att.attendancePercentage -lt 50.0) { throw "Expected attendance percentage >= 50, got $($att.attendancePercentage)" }
}

# 4. Student Timetable
Test-Scenario "STUD-TIME-01" "Student A1 View Class Timetable" {
    $res = Invoke-RestMethod -Uri "$apiUrl/api/student/timetable" -Method Get -Headers $studentA1Headers
    if ($res.data.Count -lt 1) { throw "Student timetable returned 0 slots." }
    if ($res.data[0].subjectName -ne "Mathematics") { throw "Subject mismatch: $($res.data[0].subjectName)" }
}

# 5. Student Assignments, Submissions & Grading
Test-Scenario "STUD-ASGN-01" "Student A1 View Class Assignments" {
    $res = Invoke-RestMethod -Uri "$apiUrl/api/student/assignments" -Method Get -Headers $studentA1Headers
    if ($res.data.Count -lt 1) { throw "Student assignments returned 0 records." }
    if ($res.data[0].title -notmatch "Algebra") { throw "Assignment title mismatch: $($res.data[0].title)" }
}

Test-Scenario "SEC-STUD-01" "SECURITY: Student B1 in Org B Cannot See Org A Assignments" {
    $res = Invoke-RestMethod -Uri "$apiUrl/api/student/assignments" -Method Get -Headers $studentB1Headers
    $found = $res.data | Where-Object { $_.id -eq $script:assignmentId }
    if ($found) { throw "CROSS-TENANT LEAK: Student B1 saw Org A assignment!" }
    $results.SecurityChecks["AssignmentCrossTenantIsolation"] = "PASS"
}

Test-Scenario "STUD-SUBMIT-01" "Student A1 Submit Homework Solution" {
    $payload = @{
        submissionText = "Completed all 15 quadratic equations using factoring and quadratic formula."
        attachmentUrl = "/uploads/homework/ali_raza_hw1.pdf"
        attachmentFileName = "ali_raza_hw1.pdf"
    } | ConvertTo-Json
    $res = Invoke-RestMethod -Uri "$apiUrl/api/student/assignments/$($script:assignmentId)/submit" -Method Post -Headers $studentA1Headers -Body $payload -ContentType "application/json"
    if ($res.success -ne $true) { throw "Assignment submission failed." }
}

Test-Scenario "STUD-SUBMIT-02" "Student A1 View Own Submission" {
    $res = Invoke-RestMethod -Uri "$apiUrl/api/student/assignments/$($script:assignmentId)/submission" -Method Get -Headers $studentA1Headers
    $sub = $res.data
    if ($sub.submissionStatus -ne "Submitted") { throw "Expected Submitted status, got $($sub.submissionStatus)" }
    if ($sub.submissionText -notmatch "quadratic") { throw "Submission text mismatch: $($sub.submissionText)" }
}

Test-Scenario "SEC-STUD-02" "SECURITY & IDOR: Student A2 Cannot View Student A1's Submission" {
    # Student A2 has not submitted this assignment yet
    $subA2 = Invoke-RestMethod -Uri "$apiUrl/api/student/assignments/$($script:assignmentId)/submission" -Method Get -Headers $studentA2Headers
    if ($subA2.data -ne $null) {
        throw "IDOR LEAK: Student A2 saw a submission when they haven't submitted anything!"
    }
    $results.SecurityChecks["AssignmentIDORProtection"] = "PASS"
}

Test-Scenario "TEACH-GRADE-01" "Teacher A1 Grade Student Submission" {
    $payload = @{
        studentId = $studentA1RecordId
        marksAwarded = 48.0
        teacherFeedback = "Excellent step-by-step mathematical proofs!"
    } | ConvertTo-Json
    $res = Invoke-RestMethod -Uri "$apiUrl/api/teacher/assignments/$($script:assignmentId)/grade" -Method Post -Headers $teacherA1Headers -Body $payload -ContentType "application/json"
    if ($res.success -ne $true) { throw "Teacher grading failed." }
}

Test-Scenario "STUD-GRADE-01" "Student A1 View Graded Marks & Feedback" {
    $res = Invoke-RestMethod -Uri "$apiUrl/api/student/assignments/$($script:assignmentId)/submission" -Method Get -Headers $studentA1Headers
    $sub = $res.data
    if ($sub.submissionStatus -ne "Graded") { throw "Expected Graded status, got $($sub.submissionStatus)" }
    if ($sub.marksAwarded -ne 48.0) { throw "Expected 48.0 marks, got $($sub.marksAwarded)" }
    if ($sub.teacherFeedback -notmatch "proofs") { throw "Feedback mismatch: $($sub.teacherFeedback)" }
}

# 6. Student Exam Results View
Test-Scenario "STUD-EXAM-01" "Student A1 View Upcoming Exams" {
    $res = Invoke-RestMethod -Uri "$apiUrl/api/student/exams" -Method Get -Headers $studentA1Headers
    if ($res.data.Count -lt 1) { throw "Expected scheduled exam, got 0." }
    if ($res.data[0].subjectName -ne "Mathematics") { throw "Exam subject mismatch: $($res.data[0].subjectName)" }
}

Test-Scenario "STUD-RES-01" "Student A1 View Finalized Exam Results" {
    $res = Invoke-RestMethod -Uri "$apiUrl/api/student/results" -Method Get -Headers $studentA1Headers
    if ($res.data.Count -lt 1) { throw "Expected 1 finalized result, got 0." }
    $r = $res.data[0]
    if ($r.marksObtained -ne 92.0) { throw "Expected marks 92.0, got $($r.marksObtained)" }
    if ($r.grade -ne "A+") { throw "Expected grade A+, got $($r.grade)" }
}

# 7. Student Fees
$feeInvoiceId = ""
Test-Scenario "STUD-FEE-01" "Student A1 View Own Fee Invoice" {
    # Create fee invoice via OrgAdmin
    $payload = @{
        branchId = $branchA1Id
        studentId = $studentA1RecordId
        title = "Tuition Fee - October 2026"
        totalAmount = 15000.00
        dueDate = (Get-Date).AddDays(15).ToString("yyyy-MM-dd")
    } | ConvertTo-Json
    $invRes = Invoke-RestMethod -Uri "$apiUrl/api/academics/fees/invoices" -Method Post -Headers $orgAHeaders -Body $payload -ContentType "application/json"
    $script:feeInvoiceId = $invRes.data.id

    # Student A1 queries fees
    $res = Invoke-RestMethod -Uri "$apiUrl/api/student/fees" -Method Get -Headers $studentA1Headers
    if ($res.data.Count -lt 1) { throw "Student fees returned 0 invoices." }
    if ($res.data[0].totalAmount -ne 15000.00) { throw "Invoice total amount mismatch: $($res.data[0].totalAmount)" }
}

Test-Scenario "SEC-STUD-03" "SECURITY & IDOR: Student A2 Cannot See Student A1's Fee Invoices" {
    $res = Invoke-RestMethod -Uri "$apiUrl/api/student/fees" -Method Get -Headers $studentA2Headers
    $found = $res.data | Where-Object { $_.id -eq $script:feeInvoiceId }
    if ($found) { throw "IDOR LEAK: Student A2 saw Student A1's fee invoice!" }
    $results.SecurityChecks["FeeIDORProtection"] = "PASS"
}

Test-Scenario "SEC-STUD-04" "SECURITY: Student B1 (Org B) Cannot See Org A Fee Invoices" {
    $res = Invoke-RestMethod -Uri "$apiUrl/api/student/fees" -Method Get -Headers $studentB1Headers
    $found = $res.data | Where-Object { $_.id -eq $script:feeInvoiceId }
    if ($found) { throw "CROSS-TENANT LEAK: Student B1 saw Org A fee invoice!" }
    $results.SecurityChecks["FeeCrossTenantIsolation"] = "PASS"
}

# 8. Student Documents & IDOR Protection
$studentDocId = ""
Test-Scenario "STUD-DOC-01" "Student A1 View Official Documents" {
    # Admin uploads document for Student A1
    $payload = @{
        title = "Student ID Card 2026-2027"
        documentType = 1 # IdCard
        fileUrl = "/documents/idcard-ali-raza.pdf"
        fileName = "idcard-ali-raza.pdf"
        fileSizeBytes = 204800
        description = "Official Student Identity Card"
    } | ConvertTo-Json
    $docRes = Invoke-RestMethod -Uri "$apiUrl/api/students/$($studentA1RecordId)/documents" -Method Post -Headers $orgAHeaders -Body $payload -ContentType "application/json"
    $script:studentDocId = if ($docRes.data.id) { $docRes.data.id } else { $docRes.data }

    # Student A1 queries documents
    $res = Invoke-RestMethod -Uri "$apiUrl/api/student/documents" -Method Get -Headers $studentA1Headers
    if ($res.data.Count -lt 1) { throw "Student documents returned 0 records." }
    if ($res.data[0].title -notmatch "ID Card") { throw "Document title mismatch: $($res.data[0].title)" }
}

Test-Scenario "SEC-STUD-05" "SECURITY & IDOR: Student A2 Cannot Access Student A1's Document" {
    $blocked = $false
    try {
        Invoke-RestMethod -Uri "$apiUrl/api/student/documents/$($script:studentDocId)" -Method Get -Headers $studentA2Headers -ErrorAction Stop
    } catch {
        if ($_.Exception.Response.StatusCode.value__ -in @(403, 404)) { $blocked = $true }
    }
    if (-not $blocked) { throw "IDOR LEAK: Student A2 accessed Student A1's private document!" }
    $results.SecurityChecks["DocumentIDORProtection"] = "PASS"
}

# 9. Student Leave
$studentLeaveId = ""
Test-Scenario "STUD-LEAVE-01" "Student A1 Submit Leave Request" {
    $payload = @{
        leaveType = "Medical"
        startDate = (Get-Date).AddDays(2).ToString("yyyy-MM-dd")
        endDate = (Get-Date).AddDays(3).ToString("yyyy-MM-dd")
        reason = "Severe fever and doctor recommended bed rest"
    } | ConvertTo-Json
    $res = Invoke-RestMethod -Uri "$apiUrl/api/student/leave" -Method Post -Headers $studentA1Headers -Body $payload -ContentType "application/json"
    $script:studentLeaveId = if ($res.data.id) { $res.data.id } else { $res.data }
    if (-not $script:studentLeaveId) { throw "Student leave submission failed." }
}

Test-Scenario "STUD-LEAVE-02" "OrgAdmin Decision Approves Student Leave" {
    $payload = @{ status = 2; remarks = "Approved by Class Incharge" } | ConvertTo-Json
    $res = Invoke-RestMethod -Uri "$apiUrl/api/academics/leave/$($script:studentLeaveId)/decision" -Method Put -Headers $orgAHeaders -Body $payload -ContentType "application/json"
    if ($res.success -ne $true) { throw "Leave decision failed." }

    # Student verifies status
    $leaves = Invoke-RestMethod -Uri "$apiUrl/api/student/leave" -Method Get -Headers $studentA1Headers
    $item = $leaves.data | Where-Object { $_.id -eq $script:studentLeaveId }
    $isApproved = ($item.status -eq "Approved") -or ($item.statusName -eq "Approved") -or ($item.status -eq 2)
    if (-not $isApproved) { throw "Expected Approved status, got $($item.status)" }
}

# 10. Student Announcements
Test-Scenario "STUD-ANN-01" "Student A1 Receives Relevant Announcements Only" {
    # Admin creates student announcement
    $payload = @{
        branchId = $branchA1Id
        title = "Sports Day Announcement"
        content = "Annual sports day will be held next Friday."
        targetAudience = "Students"
        priority = "Normal"
    } | ConvertTo-Json
    Invoke-RestMethod -Uri "$apiUrl/api/announcements" -Method Post -Headers $orgAHeaders -Body $payload -ContentType "application/json" | Out-Null

    # Student queries announcements
    $res = Invoke-RestMethod -Uri "$apiUrl/api/student/announcements" -Method Get -Headers $studentA1Headers
    $found = $res.data | Where-Object { $_.title -match "Sports Day" }
    if (-not $found) { throw "Student announcement not received." }
}

# -----------------------------------------------------------------------------
# PHASE 3: CROSS-ORGANIZATION & CROSS-BRANCH RIGOROUS AUDIT
# -----------------------------------------------------------------------------
Write-Host "`n>>> [PHASE 3] Cross-Organization & Cross-Branch Isolation Testing..." -ForegroundColor Magenta

Test-Scenario "SEC-CROSS-01" "Cross-Tenant: Teacher A1 Direct Request to Org B Branch (Must Fail with 403/404)" {
    $blocked = $false
    try {
        Invoke-RestMethod -Uri "$apiUrl/api/teacher/classes/$grade8BId/students" -Method Get -Headers $teacherA1Headers -ErrorAction Stop
    } catch {
        if ($_.Exception.Response.StatusCode.value__ -in @(403, 404)) { $blocked = $true }
    }
    if (-not $blocked) { throw "CROSS-TENANT LEAK: Teacher A1 accessed Org B class!" }
    $results.SecurityChecks["TeacherCrossOrgIsolation"] = "PASS"
}

Test-Scenario "SEC-CROSS-02" "Cross-Branch: Teacher A1 Direct Request to Branch A2 Grade 9 (Must Fail with 403)" {
    $blocked = $false
    try {
        Invoke-RestMethod -Uri "$apiUrl/api/teacher/classes/$grade9AId/students" -Method Get -Headers $teacherA1Headers -ErrorAction Stop
    } catch {
        if ($_.Exception.Response.StatusCode.value__ -in @(403, 404)) { $blocked = $true }
    }
    if (-not $blocked) { throw "CROSS-BRANCH LEAK: Teacher A1 accessed Branch A2 class!" }
    $results.SecurityChecks["TeacherCrossBranchIsolation"] = "PASS"
}

Test-Scenario "SEC-CROSS-03" "Cross-Tenant: Student A1 Direct Request to Org B Resources (Must Fail with 403/404)" {
    $blocked = $false
    try {
        Invoke-RestMethod -Uri "$apiUrl/api/student/documents/$($studentDocId)" -Method Get -Headers $studentB1Headers -ErrorAction Stop
    } catch {
        if ($_.Exception.Response.StatusCode.value__ -in @(403, 404)) { $blocked = $true }
    }
    if (-not $blocked) { throw "CROSS-TENANT LEAK: Student B1 accessed Org A student document!" }
    $results.SecurityChecks["StudentCrossOrgIsolation"] = "PASS"
}

# -----------------------------------------------------------------------------
# PHASE 4: REGRESSION TESTING OF EXISTING MODULES
# -----------------------------------------------------------------------------
Write-Host "`n>>> [PHASE 4] Regression Testing of SuperAdmin and OrgAdmin Existing APIs..." -ForegroundColor Magenta

Test-Scenario "REG-01" "SuperAdmin Dashboard Metrics Stability" {
    $res = Invoke-RestMethod -Uri "$apiUrl/api/organizations/superadmin-dashboard" -Method Get -Headers $saHeaders
    if ($res.data.totalOrganizations -lt 1) { throw "SuperAdmin dashboard metrics regressed." }
}

Test-Scenario "REG-02" "OrgAdmin Dashboard Metrics Stability" {
    $res = Invoke-RestMethod -Uri "$apiUrl/api/dashboard/stats" -Method Get -Headers $orgAHeaders
    if ($res.data.totalStudents -lt 1) { throw "OrgAdmin dashboard stats regressed." }
}

Test-Scenario "REG-03" "Staff List & Filtering Stability" {
    $res = Invoke-RestMethod -Uri "$apiUrl/api/staff" -Method Get -Headers $orgAHeaders
    if ($res.data.items.Count -lt 2) { throw "Staff list returned fewer than expected items." }
}

Test-Scenario "REG-04" "Students List & Filtering Stability" {
    $res = Invoke-RestMethod -Uri "$apiUrl/api/students" -Method Get -Headers $orgAHeaders
    if ($res.data.items.Count -lt 2) { throw "Students list returned fewer than expected items." }
}

# -----------------------------------------------------------------------------
# FINAL RESULTS & METRICS
# -----------------------------------------------------------------------------
Write-Host "`n================================================================================" -ForegroundColor Cyan
Write-Host "                             QC SUITE SUMMARY REPORT                            " -ForegroundColor Cyan
Write-Host "================================================================================" -ForegroundColor Cyan
Write-Host "Total Scenarios Run: $($results.TotalScenarios)" -ForegroundColor Cyan
Write-Host "Passed Scenarios:    $($results.Passed)" -ForegroundColor Green
Write-Host "Failed Scenarios:    $($results.Failed)" -ForegroundColor $(if ($results.Failed -eq 0) { "Green" } else { "Red" })
$passRate = [math]::Round(($results.Passed / $results.TotalScenarios) * 100, 2)
Write-Host "Pass Percentage:     $passRate%" -ForegroundColor $(if ($passRate -eq 100) { "Green" } else { "Red" })

Write-Host "`n--- Security Audit Verification ---" -ForegroundColor Cyan
foreach ($k in $results.SecurityChecks.Keys) {
    Write-Host "  * $($k): $($results.SecurityChecks[$k])" -ForegroundColor Green
}

if ($results.Failed -gt 0) {
    Write-Host "`n--- Bugs / Issues Encountered ---" -ForegroundColor Red
    foreach ($b in $results.Bugs) {
        Write-Host "  * $($b.Scenario): $($b.Error)" -ForegroundColor Red
    }
    exit 1
} else {
    Write-Host "`n[SUCCESS] ALL 100% OF SCENARIOS PASSED WITH ZERO SECURITY LOOPHOLES!" -ForegroundColor Green
    exit 0
}
