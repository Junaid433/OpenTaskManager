using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace OpenTaskManager.Models;

public partial class DiskInfo : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;  // e.g., "sda", "nvme0n1"

    [ObservableProperty]
    private string _displayName = string.Empty;  // e.g., "Disk 0 (C:)" or "Disk 0"

    [ObservableProperty]
    private string _model = string.Empty;  // e.g., "Samsung SSD 970 EVO Plus"

    [ObservableProperty]
    private string _type = "Unknown";  // SSD, HDD, NVMe

    [ObservableProperty]
    private long _capacity;  // Total capacity in bytes

    [ObservableProperty]
    private long _usedSpace;  // Used space in bytes

    [ObservableProperty]
    private long _freeSpace;  // Free space in bytes

    [ObservableProperty]
    private double _activeTimePercent;  // 0-100

    [ObservableProperty]
    private double _averageResponseTimeMs;

    [ObservableProperty]
    private long _readSpeed;  // bytes per second

    [ObservableProperty]
    private long _writeSpeed;  // bytes per second

    [ObservableProperty]
    private long _totalBytesRead;

    [ObservableProperty]
    private long _totalBytesWritten;

    [ObservableProperty]
    private ObservableCollection<double> _activeTimeHistory = [];

    [ObservableProperty]
    private ObservableCollection<double> _transferRateHistory = [];  // Combined read+write

    // Formatted properties
    public string ReadSpeedFormatted => FormatBytesPerSecond(ReadSpeed);
    public string WriteSpeedFormatted => FormatBytesPerSecond(WriteSpeed);
    public string CapacityFormatted => FormatBytes(Capacity);
    public string UsedSpaceFormatted => FormatBytes(UsedSpace);
    public string FreeSpaceFormatted => FormatBytes(FreeSpace);
    public string ActiveTimeFormatted => $"{ActiveTimePercent:F0}%";
    public string ResponseTimeFormatted => $"{AverageResponseTimeMs:F1} ms";
    public string TypeFormatted => Type;

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
        return $"{size:F0} {sizes[order]}";
    }

    private static string FormatBytesPerSecond(long bytes)
    {
        string[] sizes = ["B/s", "KB/s", "MB/s", "GB/s"];
        int order = 0;
        double size = bytes;
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }
        return $"{size:F1} {sizes[order]}";
    }

    partial void OnReadSpeedChanged(long value) => OnPropertyChanged(nameof(ReadSpeedFormatted));
    partial void OnWriteSpeedChanged(long value) => OnPropertyChanged(nameof(WriteSpeedFormatted));
    partial void OnCapacityChanged(long value) => OnPropertyChanged(nameof(CapacityFormatted));
    partial void OnActiveTimePercentChanged(double value) => OnPropertyChanged(nameof(ActiveTimeFormatted));
    partial void OnAverageResponseTimeMsChanged(double value) => OnPropertyChanged(nameof(ResponseTimeFormatted));
}
