using HMC.Agent.Services;
using HMC.Shared.Models;
using Microsoft.Extensions.Logging;

namespace HMC.Agent.Workers;

/// <summary>
/// 定时采集所有性能指标并通过 SignalR 推送到 Server。
/// </summary>
public class MetricsCollectionWorker : BackgroundService
{
    private readonly ILogger<MetricsCollectionWorker> _log;
    private readonly SignalRClientService _signalR;
    private readonly IPerformanceCollector _perfCollector;
    private readonly INetworkMonitor _networkMonitor;
    private readonly ISystemInfoCollector _systemInfo;
    private readonly string _deviceId;
    private readonly int _intervalMs;
    private int _cycleCount;

    public MetricsCollectionWorker(
        ILogger<MetricsCollectionWorker> log,
        SignalRClientService signalR,
        IPerformanceCollector perfCollector,
        INetworkMonitor networkMonitor,
        ISystemInfoCollector systemInfo,
        string deviceId,
        int intervalMs = 2000)
    {
        _log = log;
        _signalR = signalR;
        _perfCollector = perfCollector;
        _networkMonitor = networkMonitor;
        _systemInfo = systemInfo;
        _deviceId = deviceId;
        _intervalMs = intervalMs;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _log.LogInformation("MetricsCollectionWorker starting, interval={Interval}ms", _intervalMs);

        // Wait for SignalR to connect first
        while (!stoppingToken.IsCancellationRequested && !_signalR.IsConnected)
        {
            await Task.Delay(1000, stoppingToken);
        }

        _log.LogInformation("SignalR connected, beginning metrics collection");

        // Push initial system info immediately after connect
        await PushSystemInfoAsync();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var snapshot = new MetricsSnapshot
                {
                    DeviceId = _deviceId,
                    Timestamp = DateTime.UtcNow
                };

                // Collect all metrics in parallel
                var cpuTask = _perfCollector.GetCpuAsync(stoppingToken);
                var memTask = _perfCollector.GetMemoryAsync(stoppingToken);
                var diskTask = _perfCollector.GetDiskIOAsync(stoppingToken);
                var netTask = _perfCollector.GetNetworkAsync(stoppingToken);

                await Task.WhenAll(cpuTask, memTask, diskTask, netTask);

                snapshot.Cpu = cpuTask.Result;
                snapshot.Memory = memTask.Result;
                snapshot.DiskIO = diskTask.Result;
                snapshot.Network = netTask.Result;

                // GPU (non-blocking, may be null)
                snapshot.Gpu = _perfCollector.GetGpuMetrics();

                // Heavy data: only send every 10 cycles (~20s) to avoid message size overflow
                _cycleCount++;
                if (_cycleCount % 10 == 0)
                {
                    snapshot.TcpConnections = _networkMonitor.GetTcpConnections();
                    snapshot.Processes = _systemInfo.GetProcesses();
                }

                // Push via SignalR
                await _signalR.PushMetricsAsync(snapshot);

                _log.LogDebug("Metrics pushed: CPU={Cpu}%, Mem={Mem}%, NetIn={In}/s, NetOut={Out}/s",
                    snapshot.Cpu.TotalPercent,
                    snapshot.Memory.PercentUsed,
                    FormatBytes(snapshot.Network.InBps),
                    FormatBytes(snapshot.Network.OutBps));
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Error in metrics collection cycle");
            }

            await Task.Delay(_intervalMs, stoppingToken);
        }
    }

    private async Task PushSystemInfoAsync()
    {
        try
        {
            var overview = await _systemInfo.CollectAsync();
            overview.DeviceId = _deviceId;
            // The register already sends this, so we can skip duplicate
            _log.LogInformation("System info ready: {Os}, {Cpu}, {Ram}GB RAM",
                overview.OsName,
                overview.CpuName,
                overview.TotalMemoryBytes / (1024.0 * 1024 * 1024));
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Failed to collect system info");
        }
    }

    private static string FormatBytes(double bytesPerSec)
    {
        return bytesPerSec switch
        {
            >= 1_000_000 => $"{bytesPerSec / 1_000_000:F1} MB/s",
            >= 1_000 => $"{bytesPerSec / 1_000:F1} KB/s",
            _ => $"{bytesPerSec:F0} B/s"
        };
    }
}
