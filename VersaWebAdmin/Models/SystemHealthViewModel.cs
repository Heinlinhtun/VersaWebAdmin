namespace VersaWebAdmin.Models;

public sealed class SystemHealthViewModel
{
    public double CpuUsagePercent { get; init; }
    public long MemoryUsedBytes { get; init; }
    public long MemoryTotalBytes { get; init; }
    public double MemoryUsagePercent => Percentage(MemoryUsedBytes, MemoryTotalBytes);
    public long StorageUsedBytes { get; init; }
    public long StorageTotalBytes { get; init; }
    public double StorageUsagePercent => Percentage(StorageUsedBytes, StorageTotalBytes);
    public string StorageName { get; init; } = string.Empty;
    public DateTimeOffset MeasuredAt { get; init; } = DateTimeOffset.Now;

    private static double Percentage(long used, long total) =>
        total <= 0 ? 0 : Math.Round(used * 100d / total, 1);
}
