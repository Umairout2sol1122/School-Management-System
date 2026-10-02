@echo off
echo ========================================================
echo   SchoolSync - Terminating Services and Unlocking Ports
echo ========================================================

echo Terminating SchoolManagement.Web and SchoolManagement.Api...
taskkill /F /IM SchoolManagement.Web.exe /T >nul 2>&1
taskkill /F /IM SchoolManagement.Api.exe /T >nul 2>&1

echo Freeing ports 5090 and 5080...
for /f "tokens=5" %%a in ('netstat -aon ^| findstr :5090 ^| findstr LISTENING') do (
    if not "%%a"=="" (
        echo Killing process on port 5090 [PID: %%a]
        taskkill /F /PID %%a >nul 2>&1
    )
)

for /f "tokens=5" %%a in ('netstat -aon ^| findstr :5080 ^| findstr LISTENING') do (
    if not "%%a"=="" (
        echo Killing process on port 5080 [PID: %%a]
        taskkill /F /PID %%a >nul 2>&1
    )
)

echo.
echo All School Management processes and ports have been cleanly stopped and unlocked!
echo You can now build or run smoothly in Visual Studio.
echo ========================================================
ping 127.0.0.1 -n 2 >nul
