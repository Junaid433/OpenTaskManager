using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;

namespace OpenTaskManager.Models;

public partial class SystemInfo : ObservableObject
{
    [ObservableProperty]
    private double _cpuUsage;

    [ObservableProperty]
    private double _memoryUsage;

    [ObservableProperty]
    private long _totalMemory;

    [ObservableProperty]
    private long _usedMemory;

    [ObservableProperty]
    private long _availableMemory;

    [ObservableProperty]
    private long _cachedMemory;

    [ObservableProperty]
    private long _committedAs;

    [ObservableProperty]
    private long _commitLimit;

    [ObservableProperty]
    private long _slab;

    [ObservableProperty]
    private long _pageTables;

    [ObservableProperty]
    private long _kernelStack;

    [ObservableProperty]
    private long _hardwareReserved;
    [ObservableProperty]
    private int _memorySpeedMhz;
    [ObservableProperty]
    private int _memorySlotsUsed;
    [ObservableProperty]
    private int _memorySlotsTotal;
    [ObservableProperty]
    private string _memoryFormFactor = "Unknown";

    [ObservableProperty]
    private long _compressed;

    [ObservableProperty]
    private double _diskUsage;

    [ObservableProperty]
    private long _diskReadSpeed;

    [ObservableProperty]
    private long _diskWriteSpeed;

    [ObservableProperty]
    private double _networkUsage;

    [ObservableProperty]
    private long _networkSendSpeed;

    [ObservableProperty]
    private long _networkReceiveSpeed;

    [ObservableProperty]
    private double _gpuUsage;

    [ObservableProperty]
    private string _gpuName = "Unknown GPU";

    [ObservableProperty]
    private long _gpuMemoryTotal;

    [ObservableProperty]
    private long _gpuMemoryUsed;

    [ObservableProperty]
    private long _gpuMemoryFree;

    [ObservableProperty]
    private long _gpuMemorySharedTotal;

    [ObservableProperty]
    private long _gpuMemorySharedUsed;

    [ObservableProperty]
    private double _gpuTemperature;

    [ObservableProperty]
    private string _gpuDriverVersion = "Unknown";

    [ObservableProperty]
    private double _gpuVideoEncodeUsage;

    [ObservableProperty]
    private double _gpuVideoDecodeUsage;

    [ObservableProperty]
    private double _gpu3DUsage;

    [ObservableProperty]
    private double _gpuCopyUsage;

    [ObservableProperty]
    private int _gpuPowerUsage;

    [ObservableProperty]
    private int _gpuPowerLimit;

    [ObservableProperty]
    private int _gpuFanSpeed;

    [ObservableProperty]
    private int _gpuCoreClock;

    [ObservableProperty]
    private int _gpuMemoryClock;

    [ObservableProperty]
    private bool _gpuAvailable;

    [ObservableProperty]
    private string _cpuName = "Unknown CPU";

    [ObservableProperty]
    private int _cpuCores;

    [ObservableProperty]
    private int _cpuThreads;

    [ObservableProperty]
    private double _cpuSpeed;

    [ObservableProperty]
    private double _cpuBaseSpeed;

    [ObservableProperty]
    private int _sockets;

    [ObservableProperty]
    private int _cores;

    [ObservableProperty]
    private int _logicalProcessors;

    [ObservableProperty]
    private bool _virtualization;

    [ObservableProperty]
    private long _l1Cache;

    [ObservableProperty]
    private long _l2Cache;

    [ObservableProperty]
    private long _l3Cache;

    [ObservableProperty]
    private int _processCount;

    [ObservableProperty]
    private int _threadCount;

    [ObservableProperty]
    private int _handleCount;

    [ObservableProperty]
    private long _uptime;

    public string UptimeFormatted
    {
        get
        {
            var ts = System.TimeSpan.FromSeconds(Uptime);
            return $"{(int)ts.TotalDays}:{ts.Hours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}";
        }
    }

    public string TotalMemoryFormatted => FormatBytes(TotalMemory);
    public string UsedMemoryFormatted => FormatBytes(UsedMemory);
    public string AvailableMemoryFormatted => FormatBytes(AvailableMemory);
    public string CachedMemoryFormatted => FormatBytes(CachedMemory);
    public string CommittedFormatted => FormatBytes(CommittedAs);
    public string CommitLimitFormatted => FormatBytes(CommitLimit);
    public string CommittedAndLimitGBFormatted => $"{(CommittedAs / (1024.0 * 1024.0 * 1024.0)):F1}/{(CommitLimit / (1024.0 * 1024.0 * 1024.0)):F1} GB";
    public string SlabFormatted => FormatBytesInt(Slab);
    public string PageTablesFormatted => FormatBytesInt(PageTables);
    public string KernelStackFormatted => FormatBytes(KernelStack);
    public string HardwareReservedFormatted => FormatBytes(HardwareReserved);
    public string HardwareReservedMBFormatted => $"{(HardwareReserved / (1024.0 * 1024.0)):F0} MB";
    public string CompressedMemoryFormatted => FormatBytes(Compressed);
    public string DiskReadSpeedFormatted => FormatBytes(DiskReadSpeed) + "/s";
    public string DiskWriteSpeedFormatted => FormatBytes(DiskWriteSpeed) + "/s";
    public string NetworkSendSpeedFormatted => FormatBytes(NetworkSendSpeed) + "/s";
    public string NetworkReceiveSpeedFormatted => FormatBytes(NetworkReceiveSpeed) + "/s";

    // GPU Formatted Properties
    public double GpuMemoryTotalGB => GpuMemoryTotal / (1024.0 * 1024.0 * 1024.0);
    public double GpuMemoryUsedGB => GpuMemoryUsed / (1024.0 * 1024.0 * 1024.0);
    public double GpuMemoryFreeGB => GpuMemoryFree / (1024.0 * 1024.0 * 1024.0);
    public double GpuMemorySharedTotalGB => GpuMemorySharedTotal / (1024.0 * 1024.0 * 1024.0);
    public double GpuMemorySharedUsedGB => GpuMemorySharedUsed / (1024.0 * 1024.0 * 1024.0);
    public string GpuMemoryTotalFormatted => FormatBytes(GpuMemoryTotal);
    public string GpuMemoryUsedFormatted => FormatBytes(GpuMemoryUsed);
    public string GpuMemoryFreeFormatted => FormatBytes(GpuMemoryFree);
    public string GpuDedicatedMemoryFormatted => GpuMemoryTotal > 0 ? $"{GpuMemoryUsedGB:F1}/{GpuMemoryTotalGB:F1} GB" : "N/A";
    public string GpuSharedMemoryFormatted => GpuMemorySharedTotal > 0 ? $"{GpuMemorySharedUsedGB:F1}/{GpuMemorySharedTotalGB:F1} GB" : "N/A";
    public string GpuTemperatureFormatted => GpuAvailable ? $"{GpuTemperature:F0} °C" : "N/A";
    public string GpuPowerFormatted => GpuPowerLimit > 0 ? $"{GpuPowerUsage}W/{GpuPowerLimit}W" : "N/A";
    public string GpuFanSpeedFormatted => GpuFanSpeed >= 0 ? $"{GpuFanSpeed}%" : "N/A";
    public string GpuCoreClockFormatted => GpuCoreClock > 0 ? $"{GpuCoreClock} MHz" : "N/A";
    public string GpuMemoryClockFormatted => GpuMemoryClock > 0 ? $"{GpuMemoryClock} MHz" : "N/A";
    public double GpuMemoryUsagePercent => GpuMemoryTotal > 0 ? 100.0 * GpuMemoryUsed / GpuMemoryTotal : 0;
    public string GpuUsageWithTempFormatted => GpuAvailable ? $"{GpuUsage:F0}% ({GpuTemperature:F0}°C)" : "N/A";

    partial void OnGpuMemoryTotalChanged(long value)
    {
        OnPropertyChanged(nameof(GpuMemoryTotalGB));
        OnPropertyChanged(nameof(GpuMemoryTotalFormatted));
        OnPropertyChanged(nameof(GpuDedicatedMemoryFormatted));
        OnPropertyChanged(nameof(GpuMemoryUsagePercent));
    }

    partial void OnGpuMemoryUsedChanged(long value)
    {
        OnPropertyChanged(nameof(GpuMemoryUsedGB));
        OnPropertyChanged(nameof(GpuMemoryUsedFormatted));
        OnPropertyChanged(nameof(GpuDedicatedMemoryFormatted));
        OnPropertyChanged(nameof(GpuMemoryUsagePercent));
    }

    partial void OnGpuMemoryFreeChanged(long value)
    {
        OnPropertyChanged(nameof(GpuMemoryFreeGB));
        OnPropertyChanged(nameof(GpuMemoryFreeFormatted));
    }

    partial void OnGpuMemorySharedTotalChanged(long value)
    {
        OnPropertyChanged(nameof(GpuMemorySharedTotalGB));
        OnPropertyChanged(nameof(GpuSharedMemoryFormatted));
    }

    partial void OnGpuMemorySharedUsedChanged(long value)
    {
        OnPropertyChanged(nameof(GpuMemorySharedUsedGB));
        OnPropertyChanged(nameof(GpuSharedMemoryFormatted));
    }

    partial void OnGpuTemperatureChanged(double value)
    {
        OnPropertyChanged(nameof(GpuTemperatureFormatted));
        OnPropertyChanged(nameof(GpuUsageWithTempFormatted));
    }

    partial void OnGpuPowerUsageChanged(int value)
    {
        OnPropertyChanged(nameof(GpuPowerFormatted));
    }

    partial void OnGpuPowerLimitChanged(int value)
    {
        OnPropertyChanged(nameof(GpuPowerFormatted));
    }

    partial void OnGpuFanSpeedChanged(int value)
    {
        OnPropertyChanged(nameof(GpuFanSpeedFormatted));
    }

    partial void OnGpuCoreClockChanged(int value)
    {
        OnPropertyChanged(nameof(GpuCoreClockFormatted));
    }

    partial void OnGpuMemoryClockChanged(int value)
    {
        OnPropertyChanged(nameof(GpuMemoryClockFormatted));
    }

    partial void OnGpuUsageChanged(double value)
    {
        OnPropertyChanged(nameof(GpuUsageWithTempFormatted));
    }

    private static string FormatBytes(long bytes)
    {
        string[] sizes = new string[] { "B", "KB", "MB", "GB", "TB" };
        int order = 0;
        double size = bytes;
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }
        return $"{size:F1} {sizes[order]}";
    }
    private static string FormatBytesInt(long bytes)
    {
        string[] sizes = new string[] { "B", "KB", "MB", "GB", "TB" };
        int order = 0;
        double size = bytes;
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }
        return $"{size:F0} {sizes[order]}";
    }
    public string L1CacheFormatted => FormatBytes(L1Cache);
    public string L2CacheFormatted => FormatBytes(L2Cache);
    public string L3CacheFormatted => FormatBytes(L3Cache);

    public double UsedMemoryGB => TotalMemory == 0 ? 0 : UsedMemory / (1024.0 * 1024.0 * 1024.0);
    public double TotalMemoryGB => TotalMemory == 0 ? 0 : TotalMemory / (1024.0 * 1024.0 * 1024.0);
    public string MemorySummaryFormatted => TotalMemory == 0 ? $"{UsedMemoryFormatted} / {TotalMemoryFormatted} ({MemoryUsage:F0}%)" : $"{UsedMemoryGB:F1}/{TotalMemoryGB:F1} GB ({MemoryUsage:F0}%)";

    public string UsedMemoryGBFormatted => $"{UsedMemoryGB:F1} GB";
    public string TotalMemoryGBFormatted => $"{TotalMemoryGB:F1} GB";
    public string MemorySpeedFormatted => MemorySpeedMhz > 0 ? $"{MemorySpeedMhz} MHz" : "Unknown";
    public string MemorySlotsFormatted => MemorySlotsTotal > 0 ? $"{MemorySlotsUsed} of {MemorySlotsTotal}" : "Unknown";
    public string MemoryFormFactorFormatted => string.IsNullOrWhiteSpace(MemoryFormFactor) ? "Unknown" : MemoryFormFactor;

    partial void OnTotalMemoryChanged(long value)
    {
        OnPropertyChanged(nameof(MemorySummaryFormatted));
        OnPropertyChanged(nameof(TotalMemoryGBFormatted));
    }

    partial void OnUsedMemoryChanged(long value)
    {
        OnPropertyChanged(nameof(MemorySummaryFormatted));
        OnPropertyChanged(nameof(UsedMemoryGBFormatted));
    }

    partial void OnMemoryUsageChanged(double value)
    {
        OnPropertyChanged(nameof(MemorySummaryFormatted));
    }

    partial void OnCachedMemoryChanged(long value)
    {
        OnPropertyChanged(nameof(CachedMemoryFormatted));
    }

    partial void OnCommittedAsChanged(long value)
    {
        OnPropertyChanged(nameof(CommittedFormatted));
        OnPropertyChanged(nameof(CommittedAndLimitGBFormatted));
    }

    partial void OnCommitLimitChanged(long value)
    {
        OnPropertyChanged(nameof(CommitLimitFormatted));
        OnPropertyChanged(nameof(CommittedAndLimitGBFormatted));
    }

    partial void OnSlabChanged(long value)
    {
        OnPropertyChanged(nameof(SlabFormatted));
    }

    partial void OnPageTablesChanged(long value)
    {
        OnPropertyChanged(nameof(PageTablesFormatted));
    }

    partial void OnKernelStackChanged(long value)
    {
        OnPropertyChanged(nameof(KernelStackFormatted));
    }

    partial void OnHardwareReservedChanged(long value)
    {
        OnPropertyChanged(nameof(HardwareReservedFormatted));
        OnPropertyChanged(nameof(HardwareReservedMBFormatted));
    }

    partial void OnMemorySpeedMhzChanged(int value)
    {
        OnPropertyChanged(nameof(MemorySpeedFormatted));
    }

    partial void OnMemorySlotsUsedChanged(int value)
    {
        OnPropertyChanged(nameof(MemorySlotsFormatted));
    }

    partial void OnMemorySlotsTotalChanged(int value)
    {
        OnPropertyChanged(nameof(MemorySlotsFormatted));
    }

    partial void OnMemoryFormFactorChanged(string value)
    {
        OnPropertyChanged(nameof(MemoryFormFactorFormatted));
    }

    partial void OnCompressedChanged(long value)
    {
        OnPropertyChanged(nameof(CompressedMemoryFormatted));
    }
}

public class CpuCoreInfo
{
    public int CoreId { get; set; }
    public double Usage { get; set; }
    public double Frequency { get; set; }
}

public class PerformanceDataPoint
{
    public double Value { get; set; }
    public System.DateTime Timestamp { get; set; }
}
