using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace OpenTaskManager.Models;

public partial class UserInfo : ObservableObject
{
    [ObservableProperty]
    private string _userName = string.Empty;

    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private int _sessionId;

    [ObservableProperty]
    private string _status = "Active";

    [ObservableProperty]
    private double _cpuUsage;

    [ObservableProperty]
    private long _memoryBytes;

    [ObservableProperty]
    private long _diskReadBytes;

    [ObservableProperty]
    private long _diskWriteBytes;

    [ObservableProperty]
    private long _networkSendBytes;

    [ObservableProperty]
    private long _networkReceiveBytes;

    [ObservableProperty]
    private int _processCount;

    [ObservableProperty]
    private bool _isExpanded;

    [ObservableProperty]
    private ObservableCollection<ProcessInfo> _processes = [];

    // Formatted properties
    public string CpuFormatted => $"{CpuUsage:F1}%";
    public string MemoryFormatted => FormatBytes(MemoryBytes);
    public string DiskFormatted => $"{FormatBytes(DiskReadBytes + DiskWriteBytes)}/s";
    public string NetworkFormatted => FormatBitsPerSecond((NetworkSendBytes + NetworkReceiveBytes) * 8);
    public string ProcessCountFormatted => $"({ProcessCount})";

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024.0):F1} MB";
        return $"{bytes / (1024.0 * 1024.0 * 1024.0):F1} GB";
    }

    private static string FormatBitsPerSecond(long bitsPerSecond)
    {
        if (bitsPerSecond < 1000) return $"{bitsPerSecond} bps";
        if (bitsPerSecond < 1_000_000) return $"{bitsPerSecond / 1000.0:F0} Kbps";
        if (bitsPerSecond < 1_000_000_000) return $"{bitsPerSecond / 1_000_000.0:F0} Mbps";
        return $"{bitsPerSecond / 1_000_000_000.0:F1} Gbps";
    }
}
