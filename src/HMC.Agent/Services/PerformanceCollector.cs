using System.Diagnostics;
using System.Management;
using HMC.Shared.Models;
using Microsoft.Extensions.Logging;

namespace HMC.Agent.Services;

/// <summary>
/// 使用 WMI + PerformanceCounter 采集 CPU/内存/磁盘IO/网络/GPU 性能指标。
/// </summary>
public interface IPerformanceCollector
{
    Task<CpuMetrics> GetCpuAsync(CancellationToken ct = default);
    Task<MemoryMetrics> GetMemoryAsync(CancellationToken ct = default);
    Task<DiskIOMetrics> GetDiskIOAsync(CancellationToken ct = default);
    Task<NetworkMetrics> GetNetworkAsync(CancellationToken ct = default);
    GpuMetrics? GetGpuMetrics();
}

public class PerformanceCollector : IPerformanceCollector
{
    private readonly ILogger<PerformanceCollector> _log;

    // 上次采样的值，用于计算速率
    private DateTime _lastNetworkSample = DateTime.UtcNow;
    private readonly Dictionary<string, (long InBytes, long OutBytes)> _lastNicCounters = new();

    private DateTime _lastDiskSample = DateTime.UtcNow;
    private readonly Dictionary<string, (long ReadBytes, long WriteBytes)> _lastDiskCounters = new();

    private readonly SemaphoreSlim _lock = new(1, 1);

    public PerformanceCollector(ILogger<PerformanceCollector> log)
    {
        _log = log;
    }

    public async Task<CpuMetrics> GetCpuAsync(CancellationToken ct = default)
    {
        try
        {
            var result = new CpuMetrics();

            // 总体 CPU 使用率 via WMI
            using var searcher = new ManagementObjectSearcher(
                "SELECT PercentProcessorTime FROM Win32_PerfFormattedData_PerfOS_Processor WHERE Name='_Total'");
            foreach (var obj in searcher.Get().Cast<ManagementObject>())
            {
                result.TotalPercent = Convert.ToDouble(obj["PercentProcessorTime"]);
            }

            // 当前频率
            using var cpuSearcher = new ManagementObjectSearcher(
                "SELECT CurrentClockSpeed FROM Win32_Processor");
            foreach (var obj in cpuSearcher.Get().Cast<ManagementObject>())
            {
                result.CurrentFrequencyMhz = Convert.ToDouble(obj["CurrentClockSpeed"]);
                break;
            }

            // 每核心使用率
            using var coreSearcher = new ManagementObjectSearcher(
                "SELECT PercentProcessorTime FROM Win32_PerfFormattedData_PerfOS_Processor WHERE Name<>'_Total' AND Name<>'Idle'");
            foreach (var obj in coreSearcher.Get().Cast<ManagementObject>())
            {
                result.PerCorePercent.Add(Convert.ToDouble(obj["PercentProcessorTime"]));
            }

            return await Task.FromResult(result);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Failed to collect CPU metrics");
            return new CpuMetrics();
        }
    }

    public async Task<MemoryMetrics> GetMemoryAsync(CancellationToken ct = default)
    {
        try
        {
            var result = new MemoryMetrics();

            // OS 层面
            using var osSearcher = new ManagementObjectSearcher(
                "SELECT TotalVisibleMemorySize, FreePhysicalMemory, TotalVirtualMemorySize, FreeVirtualMemory FROM Win32_OperatingSystem");
            foreach (var obj in osSearcher.Get().Cast<ManagementObject>())
            {
                var totalKB = Convert.ToUInt64(obj["TotalVisibleMemorySize"]);
                var freeKB = Convert.ToUInt64(obj["FreeVirtualMemory"]);
                var totalVirtKB = Convert.ToUInt64(obj["TotalVirtualMemorySize"]);
                var freeVirtKB = Convert.ToUInt64(obj["FreeVirtualMemory"]);

                result.TotalMB = totalKB / 1024.0;
                result.UsedMB = (totalKB - freeKB) / 1024.0;
                result.AvailableMB = freeKB / 1024.0;
                result.PercentUsed = totalKB > 0 ? ((double)(totalKB - freeKB) / totalKB) * 100 : 0;
                result.SwapTotalMB = totalVirtKB / 1024.0;
                result.SwapUsedMB = (totalVirtKB - freeVirtKB) / 1024.0;
            }

            return await Task.FromResult(result);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Failed to collect Memory metrics");
            return new MemoryMetrics();
        }
    }

    public async Task<DiskIOMetrics> GetDiskIOAsync(CancellationToken ct = default)
    {
        try
        {
            var result = new DiskIOMetrics();
            var now = DateTime.UtcNow;
            var elapsed = (now - _lastDiskSample).TotalSeconds;
            if (elapsed <= 0) elapsed = 1;

            double totalReadBps = 0;
            double totalWriteBps = 0;
            double totalReadIops = 0;
            double totalWriteIops = 0;

            using var searcher = new ManagementObjectSearcher(
                "SELECT Name, DiskReadBytesPerSec, DiskWriteBytesPerSec, DiskReadsPerSec, DiskWritesPerSec, CurrentDiskQueueLength " +
                "FROM Win32_PerfFormattedData_PerfDisk_PhysicalDisk WHERE Name<>'_Total'");
            foreach (var obj in searcher.Get().Cast<ManagementObject>())
            {
                var name = (string)obj["Name"];
                var readBps = Convert.ToDouble(obj["DiskReadBytesPerSec"]);
                var writeBps = Convert.ToDouble(obj["DiskWriteBytesPerSec"]);
                var readIops = Convert.ToDouble(obj["DiskReadsPerSec"]);
                var writeIops = Convert.ToDouble(obj["DiskWritesPerSec"]);

                totalReadBps += readBps;
                totalWriteBps += writeBps;
                totalReadIops += readIops;
                totalWriteIops += writeIops;

                result.Disks.Add(new PerDiskMetrics
                {
                    Name = name,
                    ReadBps = readBps,
                    WriteBps = writeBps,
                    DiskTimePercent = 0 // 可扩展
                });
            }

            // 总体 Queue Depth
            using var totalSearcher = new ManagementObjectSearcher(
                "SELECT CurrentDiskQueueLength FROM Win32_PerfFormattedData_PerfDisk_PhysicalDisk WHERE Name='_Total'");
            foreach (var obj in totalSearcher.Get().Cast<ManagementObject>())
            {
                result.AvgQueueDepth = Convert.ToDouble(obj["CurrentDiskQueueLength"]);
            }

            result.ReadBps = totalReadBps;
            result.WriteBps = totalWriteBps;
            result.ReadIops = totalReadIops;
            result.WriteIops = totalWriteIops;

            _lastDiskSample = now;
            return await Task.FromResult(result);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Failed to collect Disk IO metrics");
            return new DiskIOMetrics();
        }
    }

    public async Task<NetworkMetrics> GetNetworkAsync(CancellationToken ct = default)
    {
        try
        {
            var result = new NetworkMetrics();
            var now = DateTime.UtcNow;
            var elapsed = (now - _lastNetworkSample).TotalSeconds;
            if (elapsed <= 0) elapsed = 1;

            await _lock.WaitAsync(ct);
            try
            {
                double totalInBps = 0;
                double totalOutBps = 0;

                using var searcher = new ManagementObjectSearcher(
                    "SELECT Name, Description, BytesReceivedPerSec, BytesSentPerSec, " +
                    "PacketsReceivedPerSec, PacketsSentPerSec, BytesTotalPersec " +
                    "FROM Win32_PerfFormattedData_Tcpip_NetworkInterface");

                foreach (var obj in searcher.Get().Cast<ManagementObject>())
                {
                    var name = (string)obj["Name"];
                    var desc = (string)obj["Description"];
                    var inBps = Convert.ToDouble(obj["BytesReceivedPerSec"]);
                    var outBps = Convert.ToDouble(obj["BytesSentPerSec"]);
                    var inPps = 0.0;
                    var outPps = 0.0;

                    try { inPps = Convert.ToDouble(obj["PacketsReceivedPerSec"]); } catch { }
                    try { outPps = Convert.ToDouble(obj["PacketsSentPerSec"]); } catch { }

                    totalInBps += inBps;
                    totalOutBps += outBps;

                    result.Nics.Add(new PerNicMetrics
                    {
                        Name = name,
                        Description = desc,
                        InBps = inBps,
                        OutBps = outBps,
                        InPps = inPps,
                        OutPps = outPps
                    });
                }

                result.InBps = totalInBps;
                result.OutBps = totalOutBps;
            }
            finally
            {
                _lock.Release();
            }

            _lastNetworkSample = now;
            return result;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Failed to collect Network metrics");
            return new NetworkMetrics();
        }
    }

    public GpuMetrics? GetGpuMetrics()
    {
        try
        {
            var result = new GpuMetrics();

            // 基础 GPU 信息 via WMI
            using var searcher = new ManagementObjectSearcher(
                "SELECT Name, AdapterRAM, DriverVersion FROM Win32_VideoController");
            foreach (var obj in searcher.Get().Cast<ManagementObject>())
            {
                var name = (string)obj["Name"];
                result.Gpus.Add(new PerGpuMetrics
                {
                    Name = name
                });
            }

            return result.Gpus.Count > 0 ? result : null;
        }
        catch (Exception ex)
        {
            _log.LogDebug(ex, "Failed to collect GPU metrics");
            return null;
        }
    }
}
