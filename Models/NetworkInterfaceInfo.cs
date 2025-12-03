using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace OpenTaskManager.Models;

public partial class NetworkInterfaceInfo : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;  // e.g., "eth0", "wlan0", "enp3s0"

    [ObservableProperty]
    private string _displayName = string.Empty;  // e.g., "Ethernet", "Wi-Fi"

    [ObservableProperty]
    private string _adapterName = string.Empty;  // Full adapter name

    [ObservableProperty]
    private string _connectionType = "Unknown";  // Ethernet, Wi-Fi, etc.

    [ObservableProperty]
    private string _dnsName = string.Empty;

    [ObservableProperty]
    private string _ipv4Address = string.Empty;

    [ObservableProperty]
    private string _ipv6Address = string.Empty;

    [ObservableProperty]
    private string _macAddress = string.Empty;

    [ObservableProperty]
    private long _sendSpeed;  // bytes per second

    [ObservableProperty]
    private long _receiveSpeed;  // bytes per second

    [ObservableProperty]
    private long _totalBytesSent;

    [ObservableProperty]
    private long _totalBytesReceived;

    [ObservableProperty]
    private long _linkSpeed;  // Link speed in bits per second

    [ObservableProperty]
    private bool _isUp;

    [ObservableProperty]
    private ObservableCollection<double> _throughputHistory = [];  // Combined send+receive for graph

    [ObservableProperty]
    private ObservableCollection<double> _sendHistory = [];

    [ObservableProperty]
    private ObservableCollection<double> _receiveHistory = [];

    // Formatted properties
    public string SendSpeedFormatted => FormatBitsPerSecond(SendSpeed * 8);
    public string ReceiveSpeedFormatted => FormatBitsPerSecond(ReceiveSpeed * 8);
    public string TotalSentFormatted => FormatBytes(TotalBytesSent);
    public string TotalReceivedFormatted => FormatBytes(TotalBytesReceived);
    public string LinkSpeedFormatted => FormatBitsPerSecond(LinkSpeed);

    public string SidebarSendFormatted => $"S: {FormatBitsPerSecond(SendSpeed * 8)}";
    public string SidebarReceiveFormatted => $"R: {FormatBitsPerSecond(ReceiveSpeed * 8)}";

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

    private static string FormatBitsPerSecond(long bits)
    {
        string[] sizes = ["bps", "Kbps", "Mbps", "Gbps"];
        int order = 0;
        double size = bits;
        while (size >= 1000 && order < sizes.Length - 1)
        {
            order++;
            size /= 1000;
        }
        return $"{size:F0} {sizes[order]}";
    }

    partial void OnSendSpeedChanged(long value)
    {
        OnPropertyChanged(nameof(SendSpeedFormatted));
        OnPropertyChanged(nameof(SidebarSendFormatted));
    }

    partial void OnReceiveSpeedChanged(long value)
    {
        OnPropertyChanged(nameof(ReceiveSpeedFormatted));
        OnPropertyChanged(nameof(SidebarReceiveFormatted));
    }

    partial void OnTotalBytesSentChanged(long value) => OnPropertyChanged(nameof(TotalSentFormatted));
    partial void OnTotalBytesReceivedChanged(long value) => OnPropertyChanged(nameof(TotalReceivedFormatted));
    partial void OnLinkSpeedChanged(long value) => OnPropertyChanged(nameof(LinkSpeedFormatted));
}
