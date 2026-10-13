using System.Diagnostics;
using System.Management;

namespace OneClick.App.Services;

public record ProcessEntry(int ProcessId, string FriendlyName, long MemoryBytes, double CpuPercent, bool CanClose);

public record PerformanceSnapshot(
    double CpuPercent,
    double MemoryUsedPercent,
    double DiskUsedPercent,
    int    StartupProgramCount,
    int    DaysSinceLastRestart,
    IReadOnlyList<ProcessEntry> TopProcesses);

public class PerformanceService
{
    private static readonly string[] SystemProcessNames =
    [
        "system", "idle", "registry", "smss", "csrss", "wininit", "services",
        "lsass", "svchost", "explorer", "dwm", "winlogon", "taskhostw",
        "msmpeng", "securityhealthservice", "antimalware"
    ];

    private readonly PerformanceCounter? _cpuCounter;

    public PerformanceService()
    {
        try { _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total"); }
        catch { /* unavailable on some configurations */ }
    }

    public PerformanceSnapshot GetSnapshot()
    {
        var cpu  = GetCpuPercent();
        var mem  = GetMemoryPercent();
        var disk = GetDiskPercent();
        var procs = GetTopProcesses();

        return new PerformanceSnapshot(
            cpu, mem, disk,
            GetStartupProgramCount(),
            GetDaysSinceLastRestart(),
            procs);
    }

    private double GetCpuPercent()
    {
        try { return Math.Min(100, _cpuCounter?.NextValue() ?? 0); }
        catch { return 0; }
    }

    private static double GetMemoryPercent()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT TotalVisibleMemorySize, FreePhysicalMemory FROM Win32_OperatingSystem");
            foreach (ManagementObject obj in searcher.Get())
            {
                var total = Convert.ToDouble(obj["TotalVisibleMemorySize"]);
                var free  = Convert.ToDouble(obj["FreePhysicalMemory"]);
                if (total > 0) return (total - free) / total * 100;
            }
        }
        catch { }
        return 0;
    }

    private static double GetDiskPercent()
    {
        try
        {
            var root = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)) ?? "C:\\";
            var info = new DriveInfo(root);
            return (double)(info.TotalSize - info.AvailableFreeSpace) / info.TotalSize * 100;
        }
        catch { return 0; }
    }

    private static IReadOnlyList<ProcessEntry> GetTopProcesses()
    {
        try
        {
            return Process.GetProcesses()
                .Where(p => !SystemProcessNames.Any(s => p.ProcessName.Contains(s, StringComparison.OrdinalIgnoreCase)))
                .GroupBy(p => p.ProcessName, StringComparer.OrdinalIgnoreCase)
                .Select(g =>
                {
                    var first     = g.First();
                    var totalMem  = g.Sum(p => { try { return p.WorkingSet64; } catch { return 0L; } });
                    var canClose  = g.All(p => p.SessionId == Process.GetCurrentProcess().SessionId);
                    return new ProcessEntry(first.Id, ToFriendlyName(first.ProcessName), totalMem, 0, canClose);
                })
                .OrderByDescending(p => p.MemoryBytes)
                .Take(10)
                .ToList();
        }
        catch { return []; }
    }

    private static int GetStartupProgramCount()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Run");
            return key?.ValueCount ?? 0;
        }
        catch { return 0; }
    }

    private static int GetDaysSinceLastRestart() =>
        (int)TimeSpan.FromMilliseconds(Environment.TickCount64).TotalDays;

    public bool RequestClose(int processId)
    {
        try
        {
            var proc = Process.GetProcessById(processId);
            // Only allow closing if it belongs to the current session
            if (proc.SessionId != Process.GetCurrentProcess().SessionId) return false;
            return proc.CloseMainWindow();
        }
        catch { return false; }
    }

    private static string ToFriendlyName(string processName) =>
        System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(
            processName.Replace('-', ' ').Replace('_', ' '));
}
