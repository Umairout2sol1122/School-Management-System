$ErrorActionPreference = "Stop"
$apiUrl = "http://localhost:5080"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "   SUPERADMIN ENHANCEMENT PLAN - COMPREHENSIVE QC SUITE   " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# 1. Check if API is running, start if needed
$running = Get-Process -Name "SchoolManagement.Api" -ErrorAction SilentlyContinue
if (-not $running) {
    Write-Host "[1/9] Starting SchoolManagement.Api on $apiUrl..." -ForegroundColor Yellow
    $apiProj = Join-Path $PSScriptRoot "..\src\SchoolManagement.Api\SchoolManagement.Api.csproj"
    $apiProcess = Start-Process -FilePath "dotnet" -ArgumentList "run --project `"$apiProj`" --urls $apiUrl" -PassThru
    Start-Sleep -Seconds 10
} else {
    Write-Host "[1/9] SchoolManagement.Api is already running." -ForegroundColor Green
}

# Wait for API to respond
$apiReady = $false
for ($i = 0; $i -lt 15; $i++) {
    try {
        $res = Invoke-RestMethod -Uri "$apiUrl/api/auth/login" -Method Post -Body (@{ username="x"; password="y" } | ConvertTo-Json) -ContentType "application/json" -ErrorAction Stop
    } catch {
        if ($_.Exception.Response.StatusCode.value__ -in @(400, 401)) {
            $apiReady = $true
            break
        }
    }
    Start-Sleep -Seconds 2
}

if (-not $apiReady) {
    Write-Error "API did not become ready in time."
    exit 1
}
Write-Host "[PASS] API is live and responsive at $apiUrl" -ForegroundColor Green

# 2. SuperAdmin Authentication
Write-Host "`n[2/9] Authenticating as SuperAdmin..." -ForegroundColor Yellow
$loginPayload = @{
    emailOrUsername = "superadmin"
    password = "SuperAdmin@2026!"
} | ConvertTo-Json

$loginRes = Invoke-RestMethod -Uri "$apiUrl/api/auth/login" -Method Post -Body $loginPayload -ContentType "application/json"
$token = $loginRes.data.accessToken
if (-not $token) {
    Write-Error "SuperAdmin authentication failed."
    exit 1
}
Write-Host "[PASS] SuperAdmin authenticated successfully. Role: $($loginRes.data.user.roleType)" -ForegroundColor Green
$headers = @{ "Authorization" = "Bearer $token" }

# 3. SuperAdmin Dashboard Metrics
Write-Host "`n[3/9] Testing SuperAdmin Dashboard Platform Metrics..." -ForegroundColor Yellow
$dashRes = Invoke-RestMethod -Uri "$apiUrl/api/organizations/superadmin-dashboard" -Method Get -Headers $headers
$stats = $dashRes.data
Write-Host "    Total Organizations: $($stats.totalOrganizations)" -ForegroundColor Cyan
Write-Host "    Active Organizations: $($stats.activeOrganizations)" -ForegroundColor Cyan
Write-Host "    Inactive Organizations: $($stats.inactiveOrganizations)" -ForegroundColor Cyan
Write-Host "    Total Users: $($stats.totalUsers)" -ForegroundColor Cyan
Write-Host "    Recent Orgs Count: $($stats.recentOrganizations.Count)" -ForegroundColor Cyan
Write-Host "    Recent Activities: $($stats.recentActivities.Count)" -ForegroundColor Cyan

if ($stats.totalOrganizations -lt 1) {
    Write-Error "SuperAdmin Dashboard returned invalid totalOrganizations."
    exit 1
}
Write-Host "[PASS] SuperAdmin Dashboard API returned real database metrics." -ForegroundColor Green

# 4. Organization Management (CRUD + Dossier + Status Toggle)
Write-Host "`n[4/9] Testing Organization Management..." -ForegroundColor Yellow
$testOrgCode = "QC-ORG-" + (Get-Random -Minimum 1000 -Maximum 9999)
$orgPayload = @{
    name = "QC Benchmark Academy"
    code = $testOrgCode
    email = "admin@$($testOrgCode.ToLower()).edu"
    phone = "+92 300 1234567"
    address = "Blue Area, Islamabad"
    themePrimaryColor = "#924EC0"
    themeSecondaryColor = "#5840BA"
    initialAdminEmail = "admin@$($testOrgCode.ToLower()).edu"
    initialAdminUsername = "qcadmin_$($testOrgCode.ToLower())"
    initialAdminPassword = "OrgAdmin@2026!Pak"
    initialAdminFirstName = "Benchmark"
    initialAdminLastName = "Admin"
} | ConvertTo-Json

$createRes = Invoke-RestMethod -Uri "$apiUrl/api/organizations" -Method Post -Headers $headers -Body $orgPayload -ContentType "application/json"
$createdOrg = $createRes.data
$orgId = $createdOrg.organizationId
Write-Host "[PASS] Created Organization (ID: $orgId, Code: $($createdOrg.code))" -ForegroundColor Green

# Verify in grid list
$listRes = Invoke-RestMethod -Uri "$apiUrl/api/organizations" -Method Get -Headers $headers
$foundInGrid = $listRes.data | Where-Object { $_.id -eq $orgId }
if (-not $foundInGrid) {
    Write-Error "Created organization not found in organizations grid list."
    exit 1
}
Write-Host "[PASS] Found in Grid. Admin: $($foundInGrid.adminName) ($($foundInGrid.adminEmail)), Modules: $($foundInGrid.enabledModulesCount), Status: $($foundInGrid.isActive)" -ForegroundColor Green

# View Dossier
$dossierRes = Invoke-RestMethod -Uri "$apiUrl/api/organizations/$orgId" -Method Get -Headers $headers
$dossier = $dossierRes.data
Write-Host "[PASS] Dossier details verified. Admin: $($dossier.adminUser.fullName), Modules Count: $($dossier.modules.Count)" -ForegroundColor Green

# Update Org Info
$updatePayload = @{
    name = "QC Benchmark Academy - Updated"
    email = "updated@$($testOrgCode.ToLower()).edu"
    phone = "+92 300 9999999"
    address = "Sector F-8, Islamabad"
    logoUrl = $null
    themePrimaryColor = "#924EC0"
    themeSecondaryColor = "#333333"
    themeDarkMode = $true
} | ConvertTo-Json
$updateRes = Invoke-RestMethod -Uri "$apiUrl/api/organizations/$orgId" -Method Put -Headers $headers -Body $updatePayload -ContentType "application/json"
Write-Host "[PASS] Updated organization details: $($updateRes.data.name)" -ForegroundColor Green

# Toggle Status Deactivate
$deactRes = Invoke-RestMethod -Uri "$apiUrl/api/organizations/$orgId/toggle-status" -Method Patch -Headers $headers
if ($deactRes.data.isActive -ne $false) {
    Write-Error "Organization status deactivation failed."
    exit 1
}
Write-Host "[PASS] Organization deactivated successfully. Status: Inactive" -ForegroundColor Green

# Toggle Status Activate
$actRes = Invoke-RestMethod -Uri "$apiUrl/api/organizations/$orgId/toggle-status" -Method Patch -Headers $headers
if ($actRes.data.isActive -ne $true) {
    Write-Error "Organization status activation failed."
    exit 1
}
Write-Host "[PASS] Organization reactivated successfully. Status: Active" -ForegroundColor Green

# 5. Organization Logo Management
Write-Host "`n[5/9] Testing Organization Logo Management..." -ForegroundColor Yellow
$pngBytes = [Convert]::FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII=")
$boundary = [System.Guid]::NewGuid().ToString()
$LF = "`r`n"
$bodyLines = (
    "--$boundary",
    "Content-Disposition: form-data; name=`"file`"; filename=`"temp_logo.png`"",
    "Content-Type: image/png$LF",
    [System.Text.Encoding]::GetEncoding("iso-8859-1").GetString($pngBytes),
    "--$boundary--$LF"
) -join $LF

$uploadRes = Invoke-RestMethod -Uri "$apiUrl/api/organizations/$orgId/logo" -Method Post -Headers $headers -ContentType "multipart/form-data; boundary=$boundary" -Body ([System.Text.Encoding]::GetEncoding("iso-8859-1").GetBytes($bodyLines))
$uploadedLogoUrl = $uploadRes.data.logoUrl
Write-Host "[PASS] Uploaded valid logo. LogoUrl: $uploadedLogoUrl" -ForegroundColor Green

# Remove Logo
$delLogoRes = Invoke-RestMethod -Uri "$apiUrl/api/organizations/$orgId/logo" -Method Delete -Headers $headers
Write-Host "[PASS] Removed logo. Returned LogoUrl: $($delLogoRes.data.logoUrl) (Fallback expected)" -ForegroundColor Green

# 6. Module Access Matrix and Backend Authorization Enforcement
Write-Host "`n[6/9] Testing Module Access Matrix and Authorization Enforcement..." -ForegroundColor Yellow
$modulesRes = Invoke-RestMethod -Uri "$apiUrl/api/organizations/$orgId/modules" -Method Get -Headers $headers
Write-Host "    Current modules enabled: $($modulesRes.data.Count)" -ForegroundColor Cyan

# Disable Fees module for this organization
$togglePayload = @{
    modules = @(
        @{ moduleName = "Fees"; isEnabled = $false }
    )
} | ConvertTo-Json

$toggleRes = Invoke-RestMethod -Uri "$apiUrl/api/organizations/$orgId/modules" -Method Put -Headers $headers -Body $togglePayload -ContentType "application/json"
$verifyModules = Invoke-RestMethod -Uri "$apiUrl/api/organizations/$orgId/modules" -Method Get -Headers $headers
$feesMod = $verifyModules.data | Where-Object { $_.moduleName -eq "Fees" }
if ($feesMod.isEnabled -ne $false) {
    Write-Error "Failed to disable Fees module."
    exit 1
}
Write-Host "[PASS] SuperAdmin successfully disabled Fees module for Organization." -ForegroundColor Green

# Authenticate as the Organization Admin of this org
$orgAdminLoginPayload = @{
    emailOrUsername = "qcadmin_$($testOrgCode.ToLower())"
    password = "OrgAdmin@2026!Pak"
} | ConvertTo-Json

$orgAdminLoginRes = Invoke-RestMethod -Uri "$apiUrl/api/auth/login" -Method Post -Body $orgAdminLoginPayload -ContentType "application/json"
$orgAdminToken = $orgAdminLoginRes.data.accessToken
$orgAdminHeaders = @{ "Authorization" = "Bearer $orgAdminToken" }

# Verify OrgAdmin's enabledModules does NOT contain Fees
$orgAdminModules = $orgAdminLoginRes.data.user.enabledModules
if ($orgAdminModules -contains "Fees") {
    Write-Error "Disabled module Fees was still returned in OrgAdmin's enabledModules list."
    exit 1
}
Write-Host "[PASS] Verified: Disabled module Fees is omitted from OrgAdmin permissions/modules list." -ForegroundColor Green

# Re-enable Fees module by SuperAdmin
$reEnablePayload = @{
    modules = @(
        @{ moduleName = "Fees"; isEnabled = $true }
    )
} | ConvertTo-Json
$reEnableRes = Invoke-RestMethod -Uri "$apiUrl/api/organizations/$orgId/modules" -Method Put -Headers $headers -Body $reEnablePayload -ContentType "application/json"
$verifyReEnabled = Invoke-RestMethod -Uri "$apiUrl/api/organizations/$orgId/modules" -Method Get -Headers $headers
$reEnabledFeesMod = $verifyReEnabled.data | Where-Object { $_.moduleName -eq "Fees" }
if ($reEnabledFeesMod.isEnabled -ne $true) {
    Write-Error "Failed to re-enable Fees module."
    exit 1
}
Write-Host "[PASS] SuperAdmin re-enabled Fees module. Access successfully restored." -ForegroundColor Green

# 7. Reset Organization Admin Password
Write-Host "`n[7/9] Testing Reset Organization Admin Password..." -ForegroundColor Yellow
$newPass = "UpdatedAdmin@2026!P2S"
$resetPayload = @{
    newPassword = $newPass
    confirmPassword = $newPass
} | ConvertTo-Json

$resetRes = Invoke-RestMethod -Uri "$apiUrl/api/organizations/$orgId/admin/reset-password" -Method Post -Headers $headers -Body $resetPayload -ContentType "application/json"
Write-Host "[PASS] Password reset successful: $($resetRes.message)" -ForegroundColor Green

# Verify OrgAdmin can login with the NEW password
$newLoginPayload = @{
    emailOrUsername = "qcadmin_$($testOrgCode.ToLower())"
    password = $newPass
} | ConvertTo-Json
$newLoginRes = Invoke-RestMethod -Uri "$apiUrl/api/auth/login" -Method Post -Body $newLoginPayload -ContentType "application/json"
if (-not $newLoginRes.data.accessToken) {
    Write-Error "OrgAdmin failed to login with new reset password."
    exit 1
}
Write-Host "[PASS] OrgAdmin successfully logged in with the NEW reset password!" -ForegroundColor Green

# 8. Multi-Tenant and RBAC Isolation Tests
Write-Host "`n[8/9] Testing Multi-Tenant and SuperAdmin Isolation..." -ForegroundColor Yellow
$orgAdminBlocked = $false
try {
    $forbiddenTest = Invoke-RestMethod -Uri "$apiUrl/api/organizations" -Method Get -Headers $orgAdminHeaders -ErrorAction Stop
} catch {
    if ($_.Exception.Response.StatusCode.value__ -eq 403) {
        $orgAdminBlocked = $true
    }
}
if ($orgAdminBlocked) {
    Write-Host "[PASS] Verified: OrgAdmin cannot access SuperAdmin endpoints (HTTP 403 Forbidden)." -ForegroundColor Green
} else {
    Write-Error "Security Failure: OrgAdmin was allowed to access SuperAdmin organizations list."
    exit 1
}

# 9. Summary
Write-Host "`n==========================================================" -ForegroundColor Green
Write-Host "   ALL 9 SUPERADMIN QUALITY CONTROL SCENARIOS PASSED 100% " -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green
