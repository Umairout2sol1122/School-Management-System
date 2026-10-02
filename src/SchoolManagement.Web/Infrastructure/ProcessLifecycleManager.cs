using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;

namespace SchoolManagement.Web.Infrastructure;

public static class ProcessLifecycleManager
{
    private static bool _watcherStarted = false;

    /// <summary>
    /// Ensures port 5090 and 5080 are freed and previous duplicate instances of SchoolManagement.Web are cleanly terminated.
    /// </summary>
    public static void CleanLingeringProcesses()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;

        try
        {
            var currentPid = Environment.ProcessId;

            // 1. Terminate other instances of SchoolManagement.Web and SchoolManagement.Api
            foreach (var procName in new[] { "SchoolManagement.Web", "SchoolManagement.Api" })
            {
                try
                {
                    var processes = Process.GetProcessesByName(procName);
                    foreach (var p in processes)
                    {
                        if (p.Id != currentPid)
                        {
                            try
                            {
                                p.Kill(entireProcessTree: true);
                                p.WaitForExit(1000);
                            }
                            catch { }
                        }
                    }
                }
                catch { }
            }

            // 2. Check if ports 5090 or 5080 are held by any lingering process
            FreePort(5090, currentPid);
            FreePort(5080, currentPid);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ProcessLifecycleManager] Warning during process cleanup: {ex.Message}");
        }
    }

    /// <summary>
    /// Starts a background watcher that monitors the parent process (e.g. Visual Studio devenv.exe or terminal).
    /// If the parent process exits or closes, this application terminates automatically, releasing all ports and file locks.
    /// </summary>
    public static void StartParentWatcher()
    {
        if (_watcherStarted || !RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;
        _watcherStarted = true;

        try
        {
            var parentPid = GetParentProcessId();
            if (parentPid <= 4) return;

            Process parentProcess;
            try
            {
                parentProcess = Process.GetProcessById(parentPid);
            }
            catch
            {
                return;
            }

            var parentName = parentProcess.ProcessName.ToLowerInvariant();
            // If launched from Visual Studio (devenv), VS Code (code), Rider, cmd, powershell, or wt
            var monitoredParents = new[] { "devenv", "rider64", "code", "cmd", "powershell", "pwsh", "wt" };
            bool isMonitored = monitoredParents.Any(m => parentName.Contains(m));

            if (isMonitored)
            {
                var watcherThread = new Thread(() =>
                {
                    try
                    {
                        parentProcess.WaitForExit();
                        // Parent has closed! Terminate application immediately to prevent zombie locks
                        Environment.Exit(0);
                    }
                    catch { }
                })
                {
                    IsBackground = true,
                    Name = "ParentProcessWatcherThread"
                };

                watcherThread.Start();
            }
        }
        catch { }
    }

    private static void FreePort(int port, int currentPid)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c netstat -ano | findstr :{port}",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = Process.Start(startInfo);
            if (proc == null) return;

            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(1000);

            if (string.IsNullOrWhiteSpace(output)) return;

            var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                if (line.Contains($":{port}", StringComparison.OrdinalIgnoreCase) &&
                    line.Contains("LISTENING", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length > 0 && int.TryParse(parts[^1], out var pid) && pid != currentPid && pid > 4)
                    {
                        try
                        {
                            var target = Process.GetProcessById(pid);
                            target.Kill(entireProcessTree: true);
                            target.WaitForExit(1000);
                        }
                        catch { }
                    }
                }
            }
        }
        catch { }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_BASIC_INFORMATION
    {
        public IntPtr ExitStatus;
        public IntPtr PebBaseAddress;
        public IntPtr AffinityMask;
        public IntPtr BasePriority;
        public UIntPtr UniqueProcessId;
        public IntPtr InheritedFromUniqueProcessId;
    }

    [DllImport("ntdll.dll")]
    private static extern int NtQueryInformationProcess(
        IntPtr processHandle,
        int processInformationClass,
        ref PROCESS_BASIC_INFORMATION processInformation,
        int processInformationLength,
        out int returnLength);

    private static int GetParentProcessId()
    {
        try
        {
            var pbi = new PROCESS_BASIC_INFORMATION();
            int returnLength;
            int status = NtQueryInformationProcess(
                Process.GetCurrentProcess().Handle,
                0, // ProcessBasicInformation
                ref pbi,
                Marshal.SizeOf(pbi),
                out returnLength);

            if (status == 0)
            {
                return pbi.InheritedFromUniqueProcessId.ToInt32();
            }
        }
        catch { }

        return 0;
    }
}
