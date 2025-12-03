using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenTaskManager.Models;
using OpenTaskManager.Services;

namespace OpenTaskManager.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly ISystemMonitorService _monitorService;

    [ObservableProperty]
    private int _selectedTabIndex;

    [ObservableProperty]
    private bool _isSidebarExpanded = false;

    [ObservableProperty]
    private int _selectedPerformanceTab = 0; // 0=CPU, 1=Memory, 2=Disk, 3=Network, 4=GPU

    [ObservableProperty]
    private SystemInfo _systemInfo = new();

    [ObservableProperty]
    private ObservableCollection<ProcessInfo> _processes = [];

    [ObservableProperty]
    private ProcessInfo? _selectedProcess;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private ObservableCollection<ProcessInfo> _filteredProcesses = [];

    [ObservableProperty]
    private ObservableCollection<double> _cpuHistory = [];

    [ObservableProperty]
    private ObservableCollection<double> _memoryHistory = [];

    [ObservableProperty]
    private ObservableCollection<double> _diskHistory = [];

    [ObservableProperty]
    private ObservableCollection<double> _networkHistory = [];

    // Disk and Network collections
    [ObservableProperty]
    private ObservableCollection<DiskInfo> _disks = [];

    [ObservableProperty]
    private DiskInfo? _selectedDisk;

    [ObservableProperty]
    private ObservableCollection<NetworkInterfaceInfo> _networkInterfaces = [];

    [ObservableProperty]
    private NetworkInterfaceInfo? _selectedNetworkInterface;

    [ObservableProperty]
    private string _sortColumn = "CPU";

    [ObservableProperty]
    private bool _sortAscending = false;

    private const int MaxHistoryPoints = 60;

    public MainViewModel()
    {
        _monitorService = new LinuxSystemMonitorService();
        _monitorService.SystemInfoUpdated += OnSystemInfoUpdated;
        _monitorService.ProcessesUpdated += OnProcessesUpdated;
        _monitorService.DisksUpdated += OnDisksUpdated;
        _monitorService.NetworkInterfacesUpdated += OnNetworkInterfacesUpdated;

        // Initialize history with zeros
        for (int i = 0; i < MaxHistoryPoints; i++)
        {
            CpuHistory.Add(0);
            MemoryHistory.Add(0);
            DiskHistory.Add(0);
            NetworkHistory.Add(0);
        }
    }

    public async Task InitializeAsync()
    {
        await _monitorService.StartMonitoringAsync();
    }

    public async Task CleanupAsync()
    {
        await _monitorService.StopMonitoringAsync();
    }

    private void OnSystemInfoUpdated(object? sender, SystemInfo info)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            SystemInfo = info;

            // Update histories
            AddToHistory(CpuHistory, info.CpuUsage);
            AddToHistory(MemoryHistory, info.MemoryUsage);
            AddToHistory(DiskHistory, Math.Min(100, info.DiskUsage));
            AddToHistory(NetworkHistory, Math.Min(100, (info.NetworkSendSpeed + info.NetworkReceiveSpeed) / 10_000_000.0));
        });
    }

    private void OnProcessesUpdated(object? sender, List<ProcessInfo> processes)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            Processes = new ObservableCollection<ProcessInfo>(processes);
            ApplyFilter();
        });
    }

    private void OnDisksUpdated(object? sender, List<DiskInfo> disks)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            // Update existing disks or add new ones
            foreach (var disk in disks)
            {
                var existing = Disks.FirstOrDefault(d => d.Name == disk.Name);
                if (existing != null)
                {
                    // Update existing disk
                    existing.ReadSpeed = disk.ReadSpeed;
                    existing.WriteSpeed = disk.WriteSpeed;
                    existing.ActiveTimePercent = disk.ActiveTimePercent;
                    existing.AverageResponseTimeMs = disk.AverageResponseTimeMs;
                    existing.TotalBytesRead = disk.TotalBytesRead;
                    existing.TotalBytesWritten = disk.TotalBytesWritten;
                    existing.Capacity = disk.Capacity;
                    existing.UsedSpace = disk.UsedSpace;
                    existing.FreeSpace = disk.FreeSpace;
                    existing.Model = disk.Model;
                    existing.Type = disk.Type;

                    // Update history
                    AddToHistory(existing.ActiveTimeHistory, disk.ActiveTimePercent);
                    // Transfer rate as percentage of max (100 MB/s = 100%)
                    var transferRate = (disk.ReadSpeed + disk.WriteSpeed) / 1_000_000.0; // MB/s
                    AddToHistory(existing.TransferRateHistory, Math.Min(100, transferRate));
                }
                else
                {
                    // Initialize history for new disk
                    for (int i = 0; i < MaxHistoryPoints; i++)
                    {
                        disk.ActiveTimeHistory.Add(0);
                        disk.TransferRateHistory.Add(0);
                    }
                    Disks.Add(disk);
                }
            }

            // Remove disks that no longer exist
            var diskNames = disks.Select(d => d.Name).ToHashSet();
            var toRemove = Disks.Where(d => !diskNames.Contains(d.Name)).ToList();
            foreach (var disk in toRemove)
                Disks.Remove(disk);

            // Select first disk if none selected
            if (SelectedDisk == null && Disks.Count > 0)
                SelectedDisk = Disks[0];
        });
    }

    private void OnNetworkInterfacesUpdated(object? sender, List<NetworkInterfaceInfo> interfaces)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            // Update existing interfaces or add new ones
            foreach (var iface in interfaces)
            {
                var existing = NetworkInterfaces.FirstOrDefault(n => n.Name == iface.Name);
                if (existing != null)
                {
                    // Update existing interface
                    existing.SendSpeed = iface.SendSpeed;
                    existing.ReceiveSpeed = iface.ReceiveSpeed;
                    existing.TotalBytesSent = iface.TotalBytesSent;
                    existing.TotalBytesReceived = iface.TotalBytesReceived;
                    existing.IsUp = iface.IsUp;
                    existing.LinkSpeed = iface.LinkSpeed;
                    existing.Ipv4Address = iface.Ipv4Address;
                    existing.Ipv6Address = iface.Ipv6Address;
                    existing.DnsName = iface.DnsName;
                    existing.MacAddress = iface.MacAddress;

                    // Update history - throughput as percentage of link speed or fixed max
                    var maxSpeed = existing.LinkSpeed > 0 ? existing.LinkSpeed / 8.0 : 125_000_000.0; // Default 1Gbps
                    var throughputPercent = ((iface.SendSpeed + iface.ReceiveSpeed) / maxSpeed) * 100;
                    AddToHistory(existing.ThroughputHistory, Math.Min(100, throughputPercent));
                    
                    var sendPercent = (iface.SendSpeed / maxSpeed) * 100;
                    var recvPercent = (iface.ReceiveSpeed / maxSpeed) * 100;
                    AddToHistory(existing.SendHistory, Math.Min(100, sendPercent));
                    AddToHistory(existing.ReceiveHistory, Math.Min(100, recvPercent));
                }
                else
                {
                    // Initialize history for new interface
                    for (int i = 0; i < MaxHistoryPoints; i++)
                    {
                        iface.ThroughputHistory.Add(0);
                        iface.SendHistory.Add(0);
                        iface.ReceiveHistory.Add(0);
                    }
                    NetworkInterfaces.Add(iface);
                }
            }

            // Remove interfaces that no longer exist
            var ifaceNames = interfaces.Select(i => i.Name).ToHashSet();
            var toRemove = NetworkInterfaces.Where(n => !ifaceNames.Contains(n.Name)).ToList();
            foreach (var iface in toRemove)
                NetworkInterfaces.Remove(iface);

            // Select first active interface if none selected
            if (SelectedNetworkInterface == null && NetworkInterfaces.Count > 0)
                SelectedNetworkInterface = NetworkInterfaces.FirstOrDefault(n => n.IsUp) ?? NetworkInterfaces[0];
        });
    }

    private void AddToHistory(ObservableCollection<double> history, double value)
    {
        history.Add(value);
        if (history.Count > MaxHistoryPoints)
            history.RemoveAt(0);
    }

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var filtered = string.IsNullOrWhiteSpace(SearchText)
            ? Processes
            : new ObservableCollection<ProcessInfo>(
                Processes.Where(p => p.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                                     p.Pid.ToString().Contains(SearchText)));

        // Apply sorting
        var sorted = SortColumn switch
        {
            "Name" => SortAscending ? filtered.OrderBy(p => p.Name) : filtered.OrderByDescending(p => p.Name),
            "PID" => SortAscending ? filtered.OrderBy(p => p.Pid) : filtered.OrderByDescending(p => p.Pid),
            "Status" => SortAscending ? filtered.OrderBy(p => p.Status) : filtered.OrderByDescending(p => p.Status),
            "CPU" => SortAscending ? filtered.OrderBy(p => p.CpuUsage) : filtered.OrderByDescending(p => p.CpuUsage),
            "Memory" => SortAscending ? filtered.OrderBy(p => p.MemoryBytes) : filtered.OrderByDescending(p => p.MemoryBytes),
            "Disk" => SortAscending ? filtered.OrderBy(p => p.DiskReadBytes + p.DiskWriteBytes) : filtered.OrderByDescending(p => p.DiskReadBytes + p.DiskWriteBytes),
            _ => filtered.OrderByDescending(p => p.CpuUsage)
        };

        FilteredProcesses = new ObservableCollection<ProcessInfo>(sorted);
    }

    [RelayCommand]
    private void SortBy(string column)
    {
        if (SortColumn == column)
            SortAscending = !SortAscending;
        else
        {
            SortColumn = column;
            SortAscending = false;
        }
        ApplyFilter();
    }

    [RelayCommand]
    private async Task EndTask()
    {
        if (SelectedProcess != null)
        {
            await _monitorService.KillProcessAsync(SelectedProcess.Pid);
        }
    }

    [RelayCommand]
    private async Task RunNewTask()
    {
        // This would open a dialog to run a new process
        await Task.CompletedTask;
    }

    [RelayCommand]
    private void ToggleSidebar()
    {
        IsSidebarExpanded = !IsSidebarExpanded;
    }

    [RelayCommand]
    private void SelectTab(string tabName)
    {
        SelectedTabIndex = tabName switch
        {
            "Processes" => 0,
            "Performance" => 1,
            "AppHistory" => 2,
            "StartupApps" => 3,
            "Users" => 4,
            "Details" => 5,
            "Services" => 6,
            _ => 0
        };
    }

    [RelayCommand]
    private void SelectPerformanceTab(string tabName)
    {
        SelectedPerformanceTab = tabName switch
        {
            "CPU" => 0,
            "Memory" => 1,
            "Disk" => 2,
            "Network" => 3,
            "GPU" => 4,
            _ => 0
        };
    }

    [RelayCommand]
    private void SelectDisk(DiskInfo disk)
    {
        SelectedDisk = disk;
        SelectedPerformanceTab = 2; // Switch to Disk tab
    }

    [RelayCommand]
    private void SelectNetworkInterface(NetworkInterfaceInfo networkInterface)
    {
        SelectedNetworkInterface = networkInterface;
        SelectedPerformanceTab = 3; // Switch to Network tab
    }
}
