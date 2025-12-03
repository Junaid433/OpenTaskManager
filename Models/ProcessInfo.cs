using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace OpenTaskManager.Models;

public partial class ProcessInfo : ObservableObject
{
    [ObservableProperty]
    private int _pid;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _status = "Running";

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
    private string _user = string.Empty;

    [ObservableProperty]
    private string _commandLine = string.Empty;

    [ObservableProperty]
    private int _threadCount;

    [ObservableProperty]
    private int _handleCount;

    [ObservableProperty]
    private string _priority = "Normal";

    [ObservableProperty]
    private DateTime _startTime;

    [ObservableProperty]
    private bool _isSelected;

    public string MemoryFormatted => FormatBytes(MemoryBytes);
    public string DiskReadFormatted => FormatBytes(DiskReadBytes) + "/s";
    public string DiskWriteFormatted => FormatBytes(DiskWriteBytes) + "/s";
    public string NetworkSendFormatted => FormatBytes(NetworkSendBytes) + "/s";
    public string NetworkReceiveFormatted => FormatBytes(NetworkReceiveBytes) + "/s";
    public string CpuFormatted => $"{CpuUsage:F1}%";

    private static string FormatBytes(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB", "TB"];
        int order = 0;
        double size = bytes;
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }
        return $"{size:F1} {sizes[order]}";
    }
}

public class ProcessGroup
{
    public string Name { get; set; } = string.Empty;
    public string IconPath { get; set; } = string.Empty;
    public int ProcessCount { get; set; }
    public double TotalCpuUsage { get; set; }
    public long TotalMemoryBytes { get; set; }
    public System.Collections.Generic.List<ProcessInfo> Processes { get; set; } = [];
    public bool IsExpanded { get; set; } = true;
}
