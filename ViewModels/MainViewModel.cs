using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
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

    [ObservableProperty]
    private ObservableCollection<double> _gpuHistory = [];

    [ObservableProperty]
    private ObservableCollection<double> _gpuMemoryHistory = [];

    [ObservableProperty]
    private ObservableCollection<double> _gpuVideoEncodeHistory = [];

    [ObservableProperty]
    private ObservableCollection<double> _gpuVideoDecodeHistory = [];

    // Disk and Network collections
    [ObservableProperty]
    private ObservableCollection<DiskInfo> _disks = [];

    [ObservableProperty]
    private DiskInfo? _selectedDisk;

    [ObservableProperty]
    private ObservableCollection<NetworkInterfaceInfo> _networkInterfaces = [];

    [ObservableProperty]
    private NetworkInterfaceInfo? _selectedNetworkInterface;

    // Users collection
    [ObservableProperty]
    private ObservableCollection<UserInfo> _users = [];

    [ObservableProperty]
    private UserInfo? _selectedUser;

    [ObservableProperty]
    private ObservableCollection<UserInfo> _filteredUsers = [];

    [ObservableProperty]
    private string _userSearchText = string.Empty;

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
        _monitorService.UsersUpdated += OnUsersUpdated;

        // Initialize history with zeros
        for (int i = 0; i < MaxHistoryPoints; i++)
        {
            CpuHistory.Add(0);
            MemoryHistory.Add(0);
            DiskHistory.Add(0);
            NetworkHistory.Add(0);
            GpuHistory.Add(0);
            GpuMemoryHistory.Add(0);
            GpuVideoEncodeHistory.Add(0);
            GpuVideoDecodeHistory.Add(0);
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
            
            // Update GPU histories
            AddToHistory(GpuHistory, info.GpuUsage);
            AddToHistory(GpuMemoryHistory, info.GpuMemoryUsagePercent);
            AddToHistory(GpuVideoEncodeHistory, info.GpuVideoEncodeUsage);
            AddToHistory(GpuVideoDecodeHistory, info.GpuVideoDecodeUsage);
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
                    var throughputBytes = iface.SendSpeed + iface.ReceiveSpeed;
                    var throughputPercent = (throughputBytes / maxSpeed) * 100;
                    AddToHistory(existing.ThroughputHistory, Math.Min(100, Math.Max(0, throughputPercent)));
                    
                    var sendPercent = (iface.SendSpeed / maxSpeed) * 100;
                    var recvPercent = (iface.ReceiveSpeed / maxSpeed) * 100;
                    AddToHistory(existing.SendHistory, Math.Min(100, Math.Max(0, sendPercent)));
                    AddToHistory(existing.ReceiveHistory, Math.Min(100, Math.Max(0, recvPercent)));
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

    private void OnUsersUpdated(object? sender, List<UserInfo> userList)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            // Update existing users or add new ones
            foreach (var user in userList)
            {
                var existing = Users.FirstOrDefault(u => u.UserName == user.UserName);
                if (existing != null)
                {
                    // Update existing user stats
                    existing.CpuUsage = user.CpuUsage;
                    existing.MemoryBytes = user.MemoryBytes;
                    existing.DiskReadBytes = user.DiskReadBytes;
                    existing.DiskWriteBytes = user.DiskWriteBytes;
                    existing.NetworkSendBytes = user.NetworkSendBytes;
                    existing.NetworkReceiveBytes = user.NetworkReceiveBytes;
                    existing.ProcessCount = user.ProcessCount;
                    existing.Status = user.Status;

                    // Update processes list if expanded
                    if (existing.IsExpanded)
                    {
                        existing.Processes.Clear();
                        foreach (var proc in user.Processes)
                        {
                            existing.Processes.Add(proc);
                        }
                    }
                }
                else
                {
                    Users.Add(user);
                }
            }

            // Remove users that no longer exist
            var userNames = userList.Select(u => u.UserName).ToHashSet();
            var toRemove = Users.Where(u => !userNames.Contains(u.UserName)).ToList();
            foreach (var user in toRemove)
                Users.Remove(user);

            // Select first user if none selected
            if (SelectedUser == null && Users.Count > 0)
                SelectedUser = Users[0];

            // Refresh filtered view
            ApplyUserFilter();
        });
    }

    partial void OnUserSearchTextChanged(string value)
    {
        ApplyUserFilter();
    }

    private void ApplyUserFilter()
    {
        var query = string.IsNullOrWhiteSpace(UserSearchText)
            ? Users
            : new ObservableCollection<UserInfo>(Users.Where(u =>
                (u.UserName != null && u.UserName.Contains(UserSearchText, StringComparison.OrdinalIgnoreCase)) ||
                (u.Status != null && u.Status.Contains(UserSearchText, StringComparison.OrdinalIgnoreCase))));

        // Preserve selection if possible
        var previouslySelectedUserName = SelectedUser?.UserName;

        FilteredUsers = new ObservableCollection<UserInfo>(query);

        if (previouslySelectedUserName != null)
        {
            SelectedUser = FilteredUsers.FirstOrDefault(u => u.UserName == previouslySelectedUserName) ?? SelectedUser;
        }
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
        try
        {
            var app = Avalonia.Application.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.ClassicDesktopStyleApplicationLifetime;
            if (app?.MainWindow is Window mainWindow)
            {
                var dialog = new OpenTaskManager.Views.RunTaskDialog();
                var result = await dialog.ShowDialog<bool?>(mainWindow);
                
                if (result == true && !string.IsNullOrWhiteSpace(dialog.CommandText))
                {
                    string command = dialog.CommandText;
                    if (dialog.RunAsSudo)
                    {
                        command = $"sudo {command}";
                    }
                    System.Diagnostics.Process.Start("bash", new[] { "-c", command });
                }
            }
        }
        catch
        {
            // Ignore if dialog fails
        }
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

    [RelayCommand]
    private void ToggleUserExpanded(UserInfo user)
    {
        user.IsExpanded = !user.IsExpanded;
    }

    [RelayCommand]
    private async Task DisconnectUser()
    {
        if (SelectedUser == null) return;
        
        try
        {
            // On Linux, we can use pkill to terminate all user processes
            // This is a simplified implementation - a real app would need proper confirmation dialogs
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "pkill",
                Arguments = $"-u {SelectedUser.UserName}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = System.Diagnostics.Process.Start(psi);
            process?.WaitForExit();
        }
        catch
        {
            // Ignore errors
        }
        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task ManageUserAccounts()
    {
        try
        {
            // Open system settings for user accounts
            // Try various common tools
            var tools = new[]
            {
                ("gnome-control-center", "user-accounts"),
                ("systemsettings5", "kcm_users"),
                ("users-admin", ""),
                ("kdesu", "kuser")
            };

            foreach (var (tool, args) in tools)
            {
                try
                {
                    System.Diagnostics.Process.Start(tool, args);
                    return;
                }
                catch { }
            }
        }
        catch
        {
            // Ignore if no tool available
        }
        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task OpenResourceMonitor()
    {
        string sudoUser = Environment.GetEnvironmentVariable("SUDO_USER") ?? Environment.UserName;
        try
        {
            if (sudoUser != "root")
            {
                System.Diagnostics.Process.Start("su", new[] { sudoUser, "-c", "xterm -e top" });
            }
            else
            {
                // Fallback
                System.Diagnostics.Process.Start("xterm", new[] { "-e", "top" });
            }
        }
        catch
        {
            // Show error dialog
            try
            {
                System.Diagnostics.Process.Start("zenity", new[] { "--error", "--text=xterm is not installed. Please install it with 'sudo pacman -S xterm' or equivalent for your distro." });
            }
            catch
            {
                try
                {
                    System.Diagnostics.Process.Start("notify-send", new[] { "Resource Monitor", "xterm is not installed. Please install it with 'sudo pacman -S xterm' or equivalent for your distro." });
                }
                catch
                {
                    // Ignore
                }
            }
        }
        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task Copy()
    {
        string text = "";
        switch (SelectedPerformanceTab)
        {
            case 0: // CPU
                text = $@"CPU

{SystemInfo.CpuName}

Base speed:	{SystemInfo.CpuBaseSpeed:F2} GHz
Sockets:	{SystemInfo.Sockets}
Cores:	{SystemInfo.Cores}
Logical processors:	{SystemInfo.LogicalProcessors}
Virtualization:	{(SystemInfo.Virtualization ? "Enabled" : "Disabled")}
L1 cache:	{SystemInfo.L1CacheFormatted}
L2 cache:	{SystemInfo.L2CacheFormatted}
L3 cache:	{SystemInfo.L3CacheFormatted}

Utilization	{SystemInfo.CpuUsage:F0}%
Speed	{SystemInfo.CpuSpeed:F2} GHz
Up time	{SystemInfo.UptimeFormatted}
Processes	{SystemInfo.ProcessCount}
Threads	{SystemInfo.ThreadCount}
Handles	{SystemInfo.HandleCount}
";
                break;
            case 1: // Memory
                text = $@"Memory

{SystemInfo.UsedMemoryGBFormatted} / {SystemInfo.TotalMemoryGBFormatted} ({SystemInfo.MemoryUsage:F0}%)

In use:	{SystemInfo.UsedMemoryFormatted}
Available:	{SystemInfo.AvailableMemoryFormatted}
Committed:	{SystemInfo.CommittedFormatted} / {SystemInfo.CommitLimitFormatted}
Cached:	{SystemInfo.CachedMemoryFormatted}
Paged pool:	{SystemInfo.SlabFormatted}
Non-paged pool:	{SystemInfo.PageTablesFormatted}
Speed:	{SystemInfo.MemorySpeedFormatted}
Slots used:	{SystemInfo.MemorySlotsFormatted}
Form factor:	{SystemInfo.MemoryFormFactorFormatted}
";
                break;
            case 2: // Disk
                if (SelectedDisk != null)
                {
                    text = $@"Disk

{SelectedDisk.DisplayName}

Read speed:	{SelectedDisk.ReadSpeedFormatted}/s
Write speed:	{SelectedDisk.WriteSpeedFormatted}/s
Active time:	{SelectedDisk.ActiveTimePercent:F1}%
Response time:	{SelectedDisk.AverageResponseTimeMs:F1} ms
Total read:	{SelectedDisk.TotalBytesReadFormatted}
Total written:	{SelectedDisk.TotalBytesWrittenFormatted}
Capacity:	{SelectedDisk.CapacityFormatted}
Used space:	{SelectedDisk.UsedSpaceFormatted}
Free space:	{SelectedDisk.FreeSpaceFormatted}
Type:	{SelectedDisk.Type}
";
                }
                break;
            case 3: // Network
                if (SelectedNetworkInterface != null)
                {
                    text = $@"Network

{SelectedNetworkInterface.DisplayName}

Send speed:	{SelectedNetworkInterface.SendSpeedFormatted}/s
Receive speed:	{SelectedNetworkInterface.ReceiveSpeedFormatted}/s
Total sent:	{SelectedNetworkInterface.TotalSentFormatted}
Total received:	{SelectedNetworkInterface.TotalReceivedFormatted}
Link speed:	{SelectedNetworkInterface.LinkSpeedFormatted}
IPv4 address:	{SelectedNetworkInterface.Ipv4Address}
IPv6 address:	{SelectedNetworkInterface.Ipv6Address}
DNS name:	{SelectedNetworkInterface.DnsName}
MAC address:	{SelectedNetworkInterface.MacAddress}
";
                }
                break;
            case 4: // GPU
                text = $@"GPU

{SystemInfo.GpuName}

Utilization:	{SystemInfo.GpuUsage:F0}%
Dedicated GPU memory:	{SystemInfo.GpuDedicatedMemoryFormatted}
Shared GPU memory:	{SystemInfo.GpuSharedMemoryFormatted}
GPU memory:	{SystemInfo.GpuMemoryUsedFormatted} / {SystemInfo.GpuMemoryTotalFormatted}
Temperature:	{SystemInfo.GpuTemperatureFormatted}
Power:	{SystemInfo.GpuPowerFormatted}
Fan speed:	{SystemInfo.GpuFanSpeedFormatted}
Core clock:	{SystemInfo.GpuCoreClockFormatted}
Memory clock:	{SystemInfo.GpuMemoryClockFormatted}
Driver version:	{SystemInfo.GpuDriverVersion}
";
                break;
        }

        if (!string.IsNullOrEmpty(text))
        {
            var app = Avalonia.Application.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.ClassicDesktopStyleApplicationLifetime;
            if (app?.MainWindow is Avalonia.Controls.Window window)
            {
                var topLevel = Avalonia.Controls.TopLevel.GetTopLevel(window);
                if (topLevel != null && topLevel.Clipboard != null)
                {
                    await topLevel.Clipboard.SetTextAsync(text);
                }
            }
        }
    }
}
