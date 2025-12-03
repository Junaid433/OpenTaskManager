using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OpenTaskManager.Models;

namespace OpenTaskManager.Services;

public interface ISystemMonitorService
{
    event EventHandler<SystemInfo>? SystemInfoUpdated;
    event EventHandler<List<ProcessInfo>>? ProcessesUpdated;
    event EventHandler<List<DiskInfo>>? DisksUpdated;
    event EventHandler<List<NetworkInterfaceInfo>>? NetworkInterfacesUpdated;
    event EventHandler<List<UserInfo>>? UsersUpdated;

    Task StartMonitoringAsync();
    Task StopMonitoringAsync();
    
    Task<SystemInfo> GetSystemInfoAsync();
    Task<List<ProcessInfo>> GetProcessesAsync();
    Task<List<CpuCoreInfo>> GetCpuCoresAsync();
    Task<List<DiskInfo>> GetDisksAsync();
    Task<List<NetworkInterfaceInfo>> GetNetworkInterfacesAsync();
    Task<List<UserInfo>> GetUsersAsync();
    
    Task<bool> KillProcessAsync(int pid);
    Task<bool> SetProcessPriorityAsync(int pid, string priority);
    
    int RefreshIntervalMs { get; set; }
}
