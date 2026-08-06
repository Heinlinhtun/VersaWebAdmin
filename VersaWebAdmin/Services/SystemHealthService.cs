using System.Runtime.InteropServices;
using VersaWebAdmin.Models;

namespace VersaWebAdmin.Services;

public sealed class SystemHealthService(IWebHostEnvironment environment) : ISystemHealthService
{
    public async Task<SystemHealthViewModel> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var cpuTask = GetCpuUsageAsync(cancellationToken);
        var (memoryUsed, memoryTotal) = GetMemoryUsage();
        var drive = GetApplicationDrive();

        return new SystemHealthViewModel
        {
            CpuUsagePercent = await cpuTask,
            MemoryUsedBytes = memoryUsed,
            MemoryTotalBytes = memoryTotal,
            StorageUsedBytes = drive.TotalSize - drive.AvailableFreeSpace,
            StorageTotalBytes = drive.TotalSize,
            StorageName = drive.Name,
            MeasuredAt = DateTimeOffset.Now
        };
    }

    private async Task<double> GetCpuUsageAsync(CancellationToken cancellationToken)
    {
        var first = GetCpuTimes();
        await Task.Delay(250, cancellationToken);
        var second = GetCpuTimes();
        var totalDelta = second.Total - first.Total;
        var idleDelta = second.Idle - first.Idle;
        return totalDelta <= 0 ? 0 : Math.Round(Math.Clamp((totalDelta - idleDelta) * 100d / totalDelta, 0, 100), 1);
    }

    private static (ulong Idle, ulong Total) GetCpuTimes()
    {
        if (OperatingSystem.IsWindows())
        {
            if (!GetSystemTimes(out var idle, out var kernel, out var user))
                throw new InvalidOperationException("Unable to read Windows CPU usage.");

            return (idle.Value, kernel.Value + user.Value);
        }

        if (OperatingSystem.IsLinux())
        {
            var values = File.ReadLines("/proc/stat").First().Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Skip(1).Select(ulong.Parse).ToArray();
            var idle = values[3] + (values.Length > 4 ? values[4] : 0);
            return (idle, values.Aggregate(0UL, (total, value) => total + value));
        }

        throw new PlatformNotSupportedException("System CPU monitoring is supported on Windows and Linux.");
    }

    private static (long Used, long Total) GetMemoryUsage()
    {
        if (OperatingSystem.IsWindows())
        {
            var status = new MemoryStatusEx();
            if (!GlobalMemoryStatusEx(status))
                throw new InvalidOperationException("Unable to read Windows memory usage.");

            return (checked((long)(status.TotalPhysical - status.AvailablePhysical)), checked((long)status.TotalPhysical));
        }

        if (OperatingSystem.IsLinux())
        {
            var values = File.ReadLines("/proc/meminfo")
                .Select(line => line.Split(':', 2))
                .Where(parts => parts.Length == 2)
                .ToDictionary(parts => parts[0], parts => long.Parse(parts[1].Trim().Split(' ')[0]) * 1024);
            var total = values["MemTotal"];
            var available = values.GetValueOrDefault("MemAvailable");
            return (total - available, total);
        }

        var info = GC.GetGCMemoryInfo();
        return (GC.GetTotalMemory(false), info.TotalAvailableMemoryBytes);
    }

    private DriveInfo GetApplicationDrive()
    {
        var root = Path.GetPathRoot(environment.ContentRootPath)
            ?? throw new InvalidOperationException("Unable to locate the application storage volume.");
        return new DriveInfo(root);
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetSystemTimes(out FileTime idleTime, out FileTime kernelTime, out FileTime userTime);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx([In, Out] MemoryStatusEx buffer);

    [StructLayout(LayoutKind.Sequential)]
    private struct FileTime
    {
        public uint Low;
        public uint High;
        public readonly ulong Value => ((ulong)High << 32) | Low;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private sealed class MemoryStatusEx
    {
        public uint Length = (uint)Marshal.SizeOf<MemoryStatusEx>();
        public uint MemoryLoad;
        public ulong TotalPhysical;
        public ulong AvailablePhysical;
        public ulong TotalPageFile;
        public ulong AvailablePageFile;
        public ulong TotalVirtual;
        public ulong AvailableVirtual;
        public ulong AvailableExtendedVirtual;
    }
}
