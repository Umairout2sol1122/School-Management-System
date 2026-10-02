# PowerShell Script to cleanly stop all running SchoolManagement processes and free up ports
Write-Host "Checking for running School Management processes..." -ForegroundColor Cyan

$processNames = @("SchoolManagement.Web", "SchoolManagement.Api")
$stopped = 0

foreach ($procName in $processNames) {
    $procs = Get-Process -Name $procName -ErrorAction SilentlyContinue
    if ($procs) {
        foreach ($p in $procs) {
            Write-Host "Stopping process: $($p.ProcessName) (PID: $($p.Id))" -ForegroundColor Yellow
            Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue
            $stopped++
        }
    }
}

# Also check for any dotnet processes running the SchoolManagement projects
$dotnetProcs = Get-CimInstance Win32_Process -Filter "Name = 'dotnet.exe'" -ErrorAction SilentlyContinue | Where-Object { $_.CommandLine -like "*SchoolManagement*" }
if ($dotnetProcs) {
    foreach ($dp in $dotnetProcs) {
        Write-Host "Stopping dotnet runner process (PID: $($dp.ProcessId))" -ForegroundColor Yellow
        Stop-Process -Id $dp.ProcessId -Force -ErrorAction SilentlyContinue
        $stopped++
    }
}

if ($stopped -gt 0) {
    Write-Host "Successfully stopped $stopped process(es). All files and ports are now unlocked!" -ForegroundColor Green
} else {
    Write-Host "No running School Management processes found. All ports and files are already free." -ForegroundColor Green
}
