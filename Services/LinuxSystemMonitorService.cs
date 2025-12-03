using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using OpenTaskManager.Models;

namespace OpenTaskManager.Services;

public class LinuxSystemMonitorService : ISystemMonitorService, IDisposable
{
    public event EventHandler<SystemInfo>? SystemInfoUpdated;
    public event EventHandler<List<ProcessInfo>>? ProcessesUpdated;
    public event EventHandler<List<DiskInfo>>? DisksUpdated;
    public event EventHandler<List<NetworkInterfaceInfo>>? NetworkInterfacesUpdated;

    public int RefreshIntervalMs { get; set; } = 1000;

    private CancellationTokenSource? _cts;
    private Task? _monitoringTask;

    private Dictionary<int, (long utime, long stime, DateTime timestamp)> _previousCpuTimes = [];
    private (long idle, long total, DateTime timestamp) _previousSystemCpu;
    private Dictionary<string, (long read, long write, long readTime, long writeTime, long ioTime, DateTime timestamp)> _previousDiskStats = [];
    private Dictionary<string, (long rx, long tx, DateTime timestamp)> _previousNetworkStats = [];
    private (long rx, long tx, DateTime timestamp) _previousNetworkStatsTotal;

    public async Task StartMonitoringAsync()
    {
        _cts = new CancellationTokenSource();
        _monitoringTask = Task.Run(async () =>
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    var systemInfo = await GetSystemInfoAsync();
                    var processes = await GetProcessesAsync();
                    var disks = await GetDisksAsync();
                    var networks = await GetNetworkInterfacesAsync();

                    SystemInfoUpdated?.Invoke(this, systemInfo);
                    ProcessesUpdated?.Invoke(this, processes);
                    DisksUpdated?.Invoke(this, disks);
                    NetworkInterfacesUpdated?.Invoke(this, networks);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Monitoring error: {ex.Message}");
                }

                await Task.Delay(RefreshIntervalMs, _cts.Token);
            }
        }, _cts.Token);
    }

    public Task StopMonitoringAsync()
    {
        _cts?.Cancel();
        return _monitoringTask ?? Task.CompletedTask;
    }

    public async Task<SystemInfo> GetSystemInfoAsync()
    {
        var info = new SystemInfo();

        await Task.Run(() =>
        {
            // CPU Info
            try
            {
                var cpuInfo = File.ReadAllLines("/proc/cpuinfo");
                var modelName = cpuInfo.FirstOrDefault(l => l.StartsWith("model name"))?.Split(':').LastOrDefault()?.Trim();
                info.CpuName = modelName ?? "Unknown CPU";

                info.CpuCores = cpuInfo.Count(l => l.StartsWith("processor"));
                info.CpuThreads = info.CpuCores;
                info.LogicalProcessors = info.CpuThreads;
                if (info.Sockets == 0) info.Sockets = 1; // default in case lscpu isn't available

                var mhz = cpuInfo.FirstOrDefault(l => l.StartsWith("cpu MHz"))?.Split(':').LastOrDefault()?.Trim();
                if (double.TryParse(mhz, CultureInfo.InvariantCulture, out var speed))
                    info.CpuSpeed = speed / 1000.0;
                // Attempt to read additional CPU metadata from lscpu
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "lscpu",
                        Arguments = "",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    using var proc = Process.Start(psi);
                    if (proc != null)
                    {
                        var outStr = proc.StandardOutput.ReadToEnd();
                        proc.WaitForExit(1000);

                        foreach (var line in outStr.Split('\n'))
                        {
                            var parts = line.Split(':', 2, StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length != 2) continue;
                            var key = parts[0].Trim();
                            var val = parts[1].Trim();
                            // remove trailing info like "(4 instances)" so we only parse the size itself
                            var valClean = val.Split('(')[0].Trim();

                            if (key == "Socket(s)" && int.TryParse(val, out var sockets)) info.Sockets = sockets;
                            else if (key == "Core(s) per socket" && int.TryParse(val, out var coresPerSocket)) info.Cores = coresPerSocket * info.Sockets;
                                    else if ((key == "L1d cache" || key == "L1i cache"))
                                    {
                                        if (TryParseSize(valClean, out var l1)) info.L1Cache = Math.Max(info.L1Cache, l1);
                                        else Console.WriteLine($"Could not parse L1 cache size from '{val}' (cleaned '{valClean}')");
                                    }
                                    else if (key == "L2 cache")
                                    {
                                        if (TryParseSize(valClean, out var l2)) info.L2Cache = l2;
                                        else Console.WriteLine($"Could not parse L2 cache size from '{val}' (cleaned '{valClean}')");
                                    }
                                    else if (key == "L3 cache")
                                    {
                                        if (TryParseSize(valClean, out var l3)) info.L3Cache = l3;
                                        else Console.WriteLine($"Could not parse L3 cache size from '{val}' (cleaned '{valClean}')");
                                    }
                            else if (key == "L3 cache" && TryParseSize(val, out var l3)) info.L3Cache = l3;
                            else if (key == "Virtualization" && !string.IsNullOrEmpty(val)) info.Virtualization = true;
                            else if (key == "CPU max MHz" && double.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out var maxMhz)) info.CpuBaseSpeed = maxMhz / 1000.0;
                        }
                    }
                }
                catch { }
            }
            catch { }

            // CPU Usage
            try
            {
                var stat = File.ReadAllLines("/proc/stat");
                var cpuLine = stat.FirstOrDefault(l => l.StartsWith("cpu "));
                if (cpuLine != null)
                {
                    var parts = cpuLine.Split(' ', StringSplitOptions.RemoveEmptyEntries).Skip(1).Select(long.Parse).ToArray();
                    long idle = parts[3] + parts[4];
                    long total = parts.Sum();

                    if (_previousSystemCpu.total > 0)
                    {
                        var deltaIdle = idle - _previousSystemCpu.idle;
                        var deltaTotal = total - _previousSystemCpu.total;
                        if (deltaTotal > 0)
                            info.CpuUsage = 100.0 * (1.0 - (double)deltaIdle / deltaTotal);
                    }

                    _previousSystemCpu = (idle, total, DateTime.Now);
                }
            }
            catch { }

            // Memory Info
            try
            {
                var memInfo = File.ReadAllLines("/proc/meminfo");
                long GetValue(string key) =>
                    long.Parse(memInfo.FirstOrDefault(l => l.StartsWith(key))?.Split(':').Last().Replace("kB", "").Trim() ?? "0") * 1024;

                info.TotalMemory = GetValue("MemTotal");
                info.AvailableMemory = GetValue("MemAvailable");
                info.UsedMemory = info.TotalMemory - info.AvailableMemory;
                info.MemoryUsage = 100.0 * info.UsedMemory / info.TotalMemory;

                // Additional metrics
                info.CachedMemory = GetValue("Cached");
                // Optional fields: Compressed and Hardware reserved (may not exist on all kernels)
                try
                {
                    info.Compressed = GetValue("Compressed");
                }
                catch { info.Compressed = 0; }
                try
                {
                    info.HardwareReserved = GetValue("HardwareReserved") + GetValue("HardwareCorrupted");
                }
                catch { info.HardwareReserved = 0; }
                info.CommittedAs = GetValue("Committed_AS");
                info.CommitLimit = GetValue("CommitLimit");
                info.Slab = GetValue("Slab");
                info.PageTables = GetValue("PageTables");
                info.KernelStack = GetValue("KernelStack");
                // Attempt to read DIMM info (slots, speed, form factor) via dmidecode
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "dmidecode",
                        Arguments = "-t memory",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    using var proc = Process.Start(psi);
                    if (proc != null)
                    {
                        var outStr = proc.StandardOutput.ReadToEnd();
                        proc.WaitForExit(2000);
                        var lines = outStr.Split('\n');
                        int totalSlots = 0;
                        int usedSlots = 0;
                        int maxSpeed = 0;
                        string? formFactor = null;

                        for (int i = 0; i < lines.Length; i++)
                        {
                            var line = lines[i].Trim();
                            if (line.StartsWith("Memory Device", StringComparison.OrdinalIgnoreCase))
                            {
                                totalSlots++;
                                // Gather block lines until blank
                                var j = i + 1;
                                var block = new List<string>();
                                while (j < lines.Length && !string.IsNullOrWhiteSpace(lines[j]))
                                {
                                    block.Add(lines[j].Trim());
                                    j++;
                                }
                                i = j;
                                foreach (var b in block)
                                {
                                    if (b.StartsWith("Size:", StringComparison.OrdinalIgnoreCase))
                                    {
                                        var val = b.Substring("Size:".Length).Trim();
                                        if (!val.StartsWith("No Module Installed", StringComparison.OrdinalIgnoreCase) && !val.StartsWith("Unknown", StringComparison.OrdinalIgnoreCase))
                                        {
                                            usedSlots++;
                                        }
                                    }
                                    else if (b.StartsWith("Speed:", StringComparison.OrdinalIgnoreCase) ||
                                              b.StartsWith("Configured Clock Speed:", StringComparison.OrdinalIgnoreCase) ||
                                              b.StartsWith("Configured Memory Speed:", StringComparison.OrdinalIgnoreCase))
                                    {
                                        var val = b.Split(':').Last().Trim();
                                        if (val.EndsWith("MHz", StringComparison.OrdinalIgnoreCase) ||
                                            val.EndsWith("MT/s", StringComparison.OrdinalIgnoreCase))
                                        {
                                            var unit = val.EndsWith("MHz", StringComparison.OrdinalIgnoreCase) ? "MHz" : "MT/s";
                                            var numStr = val.Substring(0, val.Length - unit.Length).Trim();
                                            if (int.TryParse(numStr, out var mhz)) maxSpeed = Math.Max(maxSpeed, mhz);
                                        }
                                    }
                                    else if (b.StartsWith("Form Factor:", StringComparison.OrdinalIgnoreCase))
                                    {
                                        var val = b.Substring("Form Factor:".Length).Trim();
                                        if (!string.IsNullOrEmpty(val) && !val.Equals("Unknown", StringComparison.OrdinalIgnoreCase) && formFactor == null)
                                            formFactor = val;
                                    }
                                }
                            }
                        }

                        info.MemorySlotsTotal = totalSlots;
                        info.MemorySlotsUsed = usedSlots;
                        info.MemorySpeedMhz = maxSpeed;
                        info.MemoryFormFactor = formFactor ?? "Unknown";
                    }
                }
                catch { /* ignore, dmidecode may not be available */ }
            }
            catch { }

            // Disk Stats
            try
            {
                var diskStats = File.ReadAllLines("/proc/diskstats");
                long totalRead = 0, totalWrite = 0;

                foreach (var line in diskStats)
                {
                    var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 14)
                    {
                        var name = parts[2];
                        if (name.StartsWith("sd") || name.StartsWith("nvme") || name.StartsWith("vd"))
                        {
                            if (!name.Any(char.IsDigit) || name.StartsWith("nvme"))
                            {
                                totalRead += long.Parse(parts[5]) * 512;
                                totalWrite += long.Parse(parts[9]) * 512;
                            }
                        }
                    }
                }

                if (_previousDiskStats.TryGetValue("total", out var prev))
                {
                    var elapsed = (DateTime.Now - prev.timestamp).TotalSeconds;
                    if (elapsed > 0)
                    {
                        info.DiskReadSpeed = (long)((totalRead - prev.read) / elapsed);
                        info.DiskWriteSpeed = (long)((totalWrite - prev.write) / elapsed);
                    }
                }

                _previousDiskStats["total"] = (totalRead, totalWrite, 0, 0, 0, DateTime.Now);
                info.DiskUsage = Math.Max(info.DiskReadSpeed, info.DiskWriteSpeed) / 1_000_000.0; // Rough %
            }
            catch { }

            // Network Stats
            try
            {
                var netDev = File.ReadAllLines("/proc/net/dev").Skip(2);
                long totalRx = 0, totalTx = 0;

                foreach (var line in netDev)
                {
                    var parts = line.Split(':', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2)
                    {
                        var values = parts[1].Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        if (values.Length >= 9)
                        {
                            totalRx += long.Parse(values[0]);
                            totalTx += long.Parse(values[8]);
                        }
                    }
                }

                var elapsed = (DateTime.Now - _previousNetworkStatsTotal.timestamp).TotalSeconds;
                if (elapsed > 0 && _previousNetworkStatsTotal.timestamp != default)
                {
                    info.NetworkReceiveSpeed = (long)((totalRx - _previousNetworkStatsTotal.rx) / elapsed);
                    info.NetworkSendSpeed = (long)((totalTx - _previousNetworkStatsTotal.tx) / elapsed);
                }

                _previousNetworkStatsTotal = (totalRx, totalTx, DateTime.Now);
            }
            catch { }

            // Uptime
            try
            {
                var uptime = File.ReadAllText("/proc/uptime").Split(' ')[0];
                info.Uptime = (long)double.Parse(uptime, CultureInfo.InvariantCulture);
            }
            catch { }

            // Process/Thread counts
            try
            {
                var procDirs = Directory.GetDirectories("/proc").Where(d => int.TryParse(Path.GetFileName(d), out _)).ToList();
                info.ProcessCount = procDirs.Count;

                // Sum up threads and handles (fd count) across processes
                int totalThreads = 0;
                long totalHandles = 0;
                foreach (var procDir in procDirs)
                {
                    try
                    {
                        var statusPath = Path.Combine(procDir, "status");
                        if (File.Exists(statusPath))
                        {
                            var statusLines = File.ReadAllLines(statusPath);
                            var threads = int.TryParse(statusLines.FirstOrDefault(l => l.StartsWith("Threads:"))?.Split(':').Last().Trim(), out var t) ? t : 0;
                            totalThreads += threads;
                        }

                        var fdDir = Path.Combine(procDir, "fd");
                        if (Directory.Exists(fdDir))
                        {
                            try
                            {
                                totalHandles += Directory.GetFiles(fdDir).Length;
                            }
                            catch { }
                        }
                    }
                    catch { }
                }

                info.ThreadCount = totalThreads;
                info.HandleCount = (int)Math.Min(totalHandles, int.MaxValue);
            }
            catch { }

            // Fallback: try reading cache sizes from sysfs if lscpu didn't populate them
            try
            {
                if (info.L1Cache == 0 || info.L2Cache == 0 || info.L3Cache == 0)
                {
                    var cacheDir = "/sys/devices/system/cpu/cpu0/cache";
                    if (Directory.Exists(cacheDir))
                    {
                        var indexDirs = Directory.GetDirectories(cacheDir, "index*", SearchOption.TopDirectoryOnly);
                        foreach (var index in indexDirs)
                        {
                            try
                            {
                                var levelPath = Path.Combine(index, "level");
                                var typePath = Path.Combine(index, "type");
                                var sizePath = Path.Combine(index, "size");

                                if (!File.Exists(levelPath) || !File.Exists(sizePath)) continue;

                                var levelText = File.ReadAllText(levelPath).Trim();
                                if (!int.TryParse(levelText, out var level)) continue;

                                var sizeText = File.ReadAllText(sizePath).Trim(); // e.g. "32K"
                                if (!TryParseSize(sizeText, out var bytes)) continue;

                                // Type may be Data, Instruction, Unified
                                var typeText = File.Exists(typePath) ? File.ReadAllText(typePath).Trim() : "";

                                switch (level)
                                {
                                    case 1:
                                        // L1 has separate data and instruction; keep max
                                        info.L1Cache = Math.Max(info.L1Cache, bytes);
                                        break;
                                    case 2:
                                        info.L2Cache = Math.Max(info.L2Cache, bytes);
                                        break;
                                    case 3:
                                        info.L3Cache = Math.Max(info.L3Cache, bytes);
                                        break;
                                }
                            }
                            catch { }
                        }
                    }
                }
            }
            catch { }
        });

        return info;
    }

    private static bool TryParseSize(string input, out long bytes)
    {
        bytes = 0;
        if (string.IsNullOrWhiteSpace(input)) return false;
        try
        {
            var s = input.Trim().ToUpperInvariant().Replace(" ", ""); // e.g., "32 KiB" -> "32KIB"

            // Find where numeric part ends
            int idx = 0;
            while (idx < s.Length && (char.IsDigit(s[idx]) || s[idx] == '.' || s[idx] == ',')) idx++;
            if (idx == 0) return false;

            var numPart = s.Substring(0, idx).Replace(",", ".");
            var unitPart = s.Substring(idx);

            if (!double.TryParse(numPart, NumberStyles.Any, CultureInfo.InvariantCulture, out var num))
                return false;

            // only keep the alphabetical prefix of the unit (strip "(4INSTANCES)" or other trailing tokens)
            var unitAlpha = new string(unitPart.TakeWhile(c => char.IsLetter(c)).ToArray());
            long multiplier = 1;
            if (string.IsNullOrWhiteSpace(unitPart) || unitPart == "B") multiplier = 1;
            else if (unitAlpha == "KIB" || unitAlpha == "K") multiplier = 1024L;
            else if (unitAlpha == "KB") multiplier = 1024L;
            else if (unitAlpha == "MIB" || unitAlpha == "M") multiplier = 1024L * 1024L;
            else if (unitAlpha == "MB") multiplier = 1024L * 1024L;
            else if (unitAlpha == "GIB" || unitAlpha == "G") multiplier = 1024L * 1024L * 1024L;
            else if (unitAlpha == "GB") multiplier = 1024L * 1024L * 1024L;
            else // fallback: try to parse last char
            {
                var last = unitAlpha.Length > 0 ? unitAlpha[unitAlpha.Length - 1] : '\0';
                switch (last)
                {
                    case 'K': multiplier = 1024L; break;
                    case 'M': multiplier = 1024L * 1024L; break;
                    case 'G': multiplier = 1024L * 1024L * 1024L; break;
                    default: multiplier = 1; break;
                }
            }

            bytes = (long)Math.Round(num * multiplier);
            return true;
        }
        catch { }
        return false;
    }

    public async Task<List<ProcessInfo>> GetProcessesAsync()
    {
        var processes = new List<ProcessInfo>();
        var hertz = 100.0; // Usually 100 on Linux

        await Task.Run(() =>
        {
            try
            {
                var procDirs = Directory.GetDirectories("/proc")
                    .Where(d => int.TryParse(Path.GetFileName(d), out _))
                    .ToList();

                foreach (var procDir in procDirs)
                {
                    try
                    {
                        var pid = int.Parse(Path.GetFileName(procDir));
                        var process = new ProcessInfo { Pid = pid };

                        // Name and Status from /proc/[pid]/status
                        var statusPath = Path.Combine(procDir, "status");
                        if (File.Exists(statusPath))
                        {
                            var statusLines = File.ReadAllLines(statusPath);
                            process.Name = statusLines.FirstOrDefault(l => l.StartsWith("Name:"))?.Split(':').Last().Trim() ?? "Unknown";
                            process.ThreadCount = int.TryParse(statusLines.FirstOrDefault(l => l.StartsWith("Threads:"))?.Split(':').Last().Trim(), out var threads) ? threads : 0;

                            var state = statusLines.FirstOrDefault(l => l.StartsWith("State:"))?.Split(':').Last().Trim().FirstOrDefault();
                            process.Status = state switch
                            {
                                'R' => "Running",
                                'S' => "Sleeping",
                                'D' => "Disk sleep",
                                'Z' => "Zombie",
                                'T' => "Stopped",
                                'I' => "Idle",
                                _ => "Unknown"
                            };

                            var uid = statusLines.FirstOrDefault(l => l.StartsWith("Uid:"))?.Split('\t', StringSplitOptions.RemoveEmptyEntries).Skip(1).FirstOrDefault();
                            if (uid != null)
                            {
                                try
                                {
                                    var passwd = File.ReadAllLines("/etc/passwd");
                                    var userLine = passwd.FirstOrDefault(l => l.Split(':')[2] == uid);
                                    process.User = userLine?.Split(':')[0] ?? uid;
                                }
                                catch { process.User = uid; }
                            }
                        }

                        // Memory from /proc/[pid]/statm
                        var statmPath = Path.Combine(procDir, "statm");
                        if (File.Exists(statmPath))
                        {
                            var statm = File.ReadAllText(statmPath).Split(' ');
                            if (statm.Length >= 2)
                            {
                                process.MemoryBytes = long.Parse(statm[1]) * 4096; // RSS in pages
                            }
                        }

                        // CPU usage from /proc/[pid]/stat
                        var statPath = Path.Combine(procDir, "stat");
                        if (File.Exists(statPath))
                        {
                            var stat = File.ReadAllText(statPath);
                            var lastParen = stat.LastIndexOf(')');
                            if (lastParen > 0)
                            {
                                var fields = stat[(lastParen + 2)..].Split(' ');
                                if (fields.Length >= 20)
                                {
                                    var utime = long.Parse(fields[11]);
                                    var stime = long.Parse(fields[12]);
                                    var startTime = long.Parse(fields[19]);

                                    if (_previousCpuTimes.TryGetValue(pid, out var prev))
                                    {
                                        var elapsed = (DateTime.Now - prev.timestamp).TotalSeconds;
                                        if (elapsed > 0)
                                        {
                                            var deltaTime = (utime + stime) - (prev.utime + prev.stime);
                                            process.CpuUsage = 100.0 * deltaTime / hertz / elapsed / Environment.ProcessorCount;
                                        }
                                    }

                                    _previousCpuTimes[pid] = (utime, stime, DateTime.Now);

                                    // Start time
                                    try
                                    {
                                        var uptime = double.Parse(File.ReadAllText("/proc/uptime").Split(' ')[0], CultureInfo.InvariantCulture);
                                        var bootTime = DateTime.Now.AddSeconds(-uptime);
                                        process.StartTime = bootTime.AddSeconds(startTime / hertz);
                                    }
                                    catch { }
                                }
                            }
                        }

                        // Command line
                        var cmdlinePath = Path.Combine(procDir, "cmdline");
                        if (File.Exists(cmdlinePath))
                        {
                            process.CommandLine = File.ReadAllText(cmdlinePath).Replace('\0', ' ').Trim();
                        }

                        // I/O stats
                        var ioPath = Path.Combine(procDir, "io");
                        if (File.Exists(ioPath))
                        {
                            try
                            {
                                var ioLines = File.ReadAllLines(ioPath);
                                var readBytes = ioLines.FirstOrDefault(l => l.StartsWith("read_bytes:"))?.Split(':').Last().Trim();
                                var writeBytes = ioLines.FirstOrDefault(l => l.StartsWith("write_bytes:"))?.Split(':').Last().Trim();
                                if (readBytes != null) process.DiskReadBytes = long.Parse(readBytes);
                                if (writeBytes != null) process.DiskWriteBytes = long.Parse(writeBytes);
                            }
                            catch { } // IO file may not be readable for all processes
                        }

                        processes.Add(process);
                    }
                    catch { } // Skip processes we can't read
                }

                // Clean up old CPU times for dead processes
                var currentPids = processes.Select(p => p.Pid).ToHashSet();
                var deadPids = _previousCpuTimes.Keys.Where(p => !currentPids.Contains(p)).ToList();
                foreach (var pid in deadPids)
                    _previousCpuTimes.Remove(pid);
            }
            catch { }
        });

        return processes.OrderByDescending(p => p.CpuUsage).ToList();
    }

    public async Task<List<CpuCoreInfo>> GetCpuCoresAsync()
    {
        var cores = new List<CpuCoreInfo>();

        await Task.Run(() =>
        {
            try
            {
                var stat = File.ReadAllLines("/proc/stat");
                var cpuLines = stat.Where(l => l.StartsWith("cpu") && l.Length > 3 && char.IsDigit(l[3])).ToList();

                foreach (var line in cpuLines)
                {
                    var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    var coreId = int.Parse(parts[0][3..]);
                    var values = parts.Skip(1).Select(long.Parse).ToArray();

                    long idle = values[3] + values[4];
                    long total = values.Sum();

                    // For simplicity, just calculate current usage
                    cores.Add(new CpuCoreInfo
                    {
                        CoreId = coreId,
                        Usage = 100.0 * (1.0 - (double)idle / total),
                        Frequency = 0
                    });
                }
            }
            catch { }
        });

        return cores;
    }

    public async Task<List<DiskInfo>> GetDisksAsync()
    {
        var disks = new List<DiskInfo>();

        await Task.Run(() =>
        {
            try
            {
                // Read /proc/diskstats for I/O statistics
                var diskStats = File.ReadAllLines("/proc/diskstats");
                var diskIndex = 0;

                foreach (var line in diskStats)
                {
                    var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 14) continue;

                    var name = parts[2];
                    
                    // Only include whole disks, not partitions
                    // For NVMe: nvme0n1 (not nvme0n1p1)
                    // For SATA/SCSI: sda, sdb (not sda1, sda2)
                    // For VirtIO: vda (not vda1)
                    bool isDisk = false;
                    if (name.StartsWith("nvme") && name.Contains("n") && !name.Contains("p"))
                        isDisk = true;
                    else if ((name.StartsWith("sd") || name.StartsWith("vd") || name.StartsWith("hd")) && !name.Any(char.IsDigit))
                        isDisk = true;

                    if (!isDisk) continue;

                    var disk = new DiskInfo
                    {
                        Name = name,
                        DisplayName = $"Disk {diskIndex}"
                    };

                    // Parse disk stats
                    // Fields: major minor name reads_completed reads_merged sectors_read time_reading 
                    //         writes_completed writes_merged sectors_written time_writing
                    //         io_in_progress time_io weighted_time_io
                    long sectorsRead = long.Parse(parts[5]);
                    long sectorsWritten = long.Parse(parts[9]);
                    long timeReading = long.Parse(parts[6]);  // ms
                    long timeWriting = long.Parse(parts[10]); // ms
                    long ioTime = long.Parse(parts[12]);      // ms spent doing I/O

                    disk.TotalBytesRead = sectorsRead * 512;
                    disk.TotalBytesWritten = sectorsWritten * 512;

                    // Calculate speeds from previous readings
                    if (_previousDiskStats.TryGetValue(name, out var prev))
                    {
                        var elapsed = (DateTime.Now - prev.timestamp).TotalSeconds;
                        if (elapsed > 0)
                        {
                            disk.ReadSpeed = (long)((sectorsRead * 512 - prev.read) / elapsed);
                            disk.WriteSpeed = (long)((sectorsWritten * 512 - prev.write) / elapsed);

                            // Active time percentage (time_io is cumulative ms)
                            var ioTimeDelta = ioTime - prev.ioTime;
                            disk.ActiveTimePercent = Math.Min(100, ioTimeDelta / (elapsed * 10)); // Convert ms to %

                            // Average response time
                            var totalTime = (timeReading - prev.readTime) + (timeWriting - prev.writeTime);
                            var readsCompleted = long.Parse(parts[3]);
                            var writesCompleted = long.Parse(parts[7]);
                            // We'd need previous values for accurate calculation, simplified here
                            disk.AverageResponseTimeMs = ioTimeDelta > 0 ? ioTimeDelta / 10.0 : 0;
                        }
                    }

                    _previousDiskStats[name] = (sectorsRead * 512, sectorsWritten * 512, timeReading, timeWriting, ioTime, DateTime.Now);

                    // Get disk model and type from /sys/block
                    try
                    {
                        var modelPath = $"/sys/block/{name}/device/model";
                        if (File.Exists(modelPath))
                            disk.Model = File.ReadAllText(modelPath).Trim();
                        else
                            disk.Model = name.ToUpper();

                        // Determine disk type
                        var rotationalPath = $"/sys/block/{name}/queue/rotational";
                        if (File.Exists(rotationalPath))
                        {
                            var rotational = File.ReadAllText(rotationalPath).Trim();
                            if (name.StartsWith("nvme"))
                                disk.Type = "SSD (NVMe)";
                            else if (rotational == "0")
                                disk.Type = "SSD";
                            else
                                disk.Type = "HDD";
                        }
                    }
                    catch { }

                    // Get capacity from /sys/block/{name}/size (in 512-byte sectors)
                    try
                    {
                        var sizePath = $"/sys/block/{name}/size";
                        if (File.Exists(sizePath))
                        {
                            var sectors = long.Parse(File.ReadAllText(sizePath).Trim());
                            disk.Capacity = sectors * 512;
                        }
                    }
                    catch { }

                    // Get usage from mounted filesystems
                    try
                    {
                        var mounts = File.ReadAllLines("/proc/mounts");
                        long totalUsed = 0, totalFree = 0;
                        foreach (var mount in mounts)
                        {
                            var mountParts = mount.Split(' ');
                            if (mountParts.Length >= 2 && mountParts[0].Contains(name))
                            {
                                var mountPoint = mountParts[1];
                                try
                                {
                                    var driveInfo = new DriveInfo(mountPoint);
                                    if (driveInfo.IsReady)
                                    {
                                        totalUsed += driveInfo.TotalSize - driveInfo.AvailableFreeSpace;
                                        totalFree += driveInfo.AvailableFreeSpace;
                                    }
                                }
                                catch { }
                            }
                        }
                        disk.UsedSpace = totalUsed;
                        disk.FreeSpace = totalFree;

                        // Determine if system disk
                        disk.IsSystemDisk = false;
                        foreach (var mount in mounts)
                        {
                            var mountParts = mount.Split(' ');
                            if (mountParts.Length >= 2 && mountParts[0].Contains(disk.Name) && mountParts[1] == "/")
                            {
                                disk.IsSystemDisk = true;
                                break;
                            }
                        }

                        // Determine if page file (swap)
                        disk.IsPageFile = false;
                        try
                        {
                            var swaps = File.ReadAllLines("/proc/swaps");
                            foreach (var swap in swaps.Skip(1))
                            {
                                var swapParts = swap.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                                if (swapParts.Length >= 1 && swapParts[0].Contains(disk.Name))
                                {
                                    disk.IsPageFile = true;
                                    break;
                                }
                            }
                        }
                        catch { }
                    }
                    catch { }

                    disks.Add(disk);
                    diskIndex++;
                }
            }
            catch { }
        });

        return disks;
    }

    public async Task<List<NetworkInterfaceInfo>> GetNetworkInterfacesAsync()
    {
        var interfaces = new List<NetworkInterfaceInfo>();

        await Task.Run(() =>
        {
            try
            {
                var netDev = File.ReadAllLines("/proc/net/dev").Skip(2);

                foreach (var line in netDev)
                {
                    var parts = line.Split(':', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 2) continue;

                    var name = parts[0].Trim();
                    
                    // Skip loopback interface
                    if (name == "lo") continue;

                    var values = parts[1].Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (values.Length < 9) continue;

                    var iface = new NetworkInterfaceInfo
                    {
                        Name = name,
                        TotalBytesReceived = long.Parse(values[0]),
                        TotalBytesSent = long.Parse(values[8])
                    };

                    // Determine connection type and display name from interface name
                    if (name.StartsWith("wl") || name.StartsWith("wlan") || name.Contains("wifi"))
                    {
                        iface.ConnectionType = "Wi-Fi";
                        iface.DisplayName = "Wi-Fi";
                    }
                    else if (name.StartsWith("eth") || name.StartsWith("en") || name.StartsWith("em"))
                    {
                        iface.ConnectionType = "Ethernet";
                        iface.DisplayName = "Ethernet";
                    }
                    else if (name.StartsWith("docker") || name.StartsWith("br-") || name.StartsWith("veth"))
                    {
                        iface.ConnectionType = "Virtual";
                        iface.DisplayName = "Docker Network";
                    }
                    else if (name.StartsWith("virbr") || name.StartsWith("vnet"))
                    {
                        iface.ConnectionType = "Virtual";
                        iface.DisplayName = "Virtual Bridge";
                    }
                    else if (name.StartsWith("tun") || name.StartsWith("tap"))
                    {
                        iface.ConnectionType = "VPN";
                        iface.DisplayName = "VPN";
                    }
                    else
                    {
                        iface.ConnectionType = "Unknown";
                        iface.DisplayName = name;
                    }

                    // Get adapter name from /sys/class/net
                    try
                    {
                        // Check if interface is up
                        var operstatePath = $"/sys/class/net/{name}/operstate";
                        if (File.Exists(operstatePath))
                        {
                            var state = File.ReadAllText(operstatePath).Trim();
                            iface.IsUp = state == "up";
                        }

                        // Get link speed
                        var speedPath = $"/sys/class/net/{name}/speed";
                        if (File.Exists(speedPath))
                        {
                            try
                            {
                                var speedMbps = int.Parse(File.ReadAllText(speedPath).Trim());
                                if (speedMbps > 0)
                                    iface.LinkSpeed = speedMbps * 1_000_000L; // Convert Mbps to bps
                            }
                            catch { }
                        }

                        // Get MAC address
                        var addressPath = $"/sys/class/net/{name}/address";
                        if (File.Exists(addressPath))
                            iface.MacAddress = File.ReadAllText(addressPath).Trim().ToUpper();

                        // Try to get a more descriptive adapter name
                        var devicePath = $"/sys/class/net/{name}/device";
                        if (Directory.Exists(devicePath))
                        {
                            // Try vendor/device for PCI devices
                            var vendorPath = Path.Combine(devicePath, "vendor");
                            var deviceIdPath = Path.Combine(devicePath, "device");
                            if (File.Exists(vendorPath) && File.Exists(deviceIdPath))
                            {
                                // Could look up vendor/device IDs in PCI database, simplified here
                                iface.AdapterName = $"{iface.ConnectionType} Adapter ({name})";
                            }
                        }
                        if (string.IsNullOrEmpty(iface.AdapterName))
                            iface.AdapterName = $"{iface.ConnectionType} Adapter";
                    }
                    catch { }

                    // Calculate speeds from previous readings
                    if (_previousNetworkStats.TryGetValue(name, out var prev))
                    {
                        var elapsed = (DateTime.Now - prev.timestamp).TotalSeconds;
                        if (elapsed > 0)
                        {
                            iface.ReceiveSpeed = (long)((iface.TotalBytesReceived - prev.rx) / elapsed);
                            iface.SendSpeed = (long)((iface.TotalBytesSent - prev.tx) / elapsed);
                        }
                    }

                    _previousNetworkStats[name] = (iface.TotalBytesReceived, iface.TotalBytesSent, DateTime.Now);

                    // Get IP addresses using .NET NetworkInterface
                    try
                    {
                        var netInterface = NetworkInterface.GetAllNetworkInterfaces()
                            .FirstOrDefault(n => n.Name == name);
                        
                        if (netInterface != null)
                        {
                            var ipProps = netInterface.GetIPProperties();
                            
                            foreach (var addr in ipProps.UnicastAddresses)
                            {
                                if (addr.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                                    iface.Ipv4Address = addr.Address.ToString();
                                else if (addr.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
                                    iface.Ipv6Address = addr.Address.ToString();
                            }

                            // DNS suffix
                            iface.DnsName = ipProps.DnsSuffix;
                        }
                    }
                    catch { }

                    interfaces.Add(iface);
                }
            }
            catch { }
        });

        // Sort: Put active interfaces first, then by connection type
        return interfaces
            .OrderByDescending(i => i.IsUp)
            .ThenBy(i => i.ConnectionType switch
            {
                "Ethernet" => 0,
                "Wi-Fi" => 1,
                "VPN" => 2,
                _ => 3
            })
            .ToList();
    }

    public async Task<bool> KillProcessAsync(int pid)
    {
        return await Task.Run(() =>
        {
            try
            {
                var process = Process.GetProcessById(pid);
                process.Kill();
                return true;
            }
            catch
            {
                return false;
            }
        });
    }

    public async Task<bool> SetProcessPriorityAsync(int pid, string priority)
    {
        return await Task.Run(() =>
        {
            try
            {
                var niceness = priority switch
                {
                    "Realtime" => -20,
                    "High" => -10,
                    "AboveNormal" => -5,
                    "Normal" => 0,
                    "BelowNormal" => 5,
                    "Low" => 10,
                    _ => 0
                };

                var psi = new ProcessStartInfo
                {
                    FileName = "renice",
                    Arguments = $"{niceness} -p {pid}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var process = Process.Start(psi);
                process?.WaitForExit();
                return process?.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        });
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }
}
