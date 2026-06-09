namespace HMC.Shared.Models;

/// <summary>
/// Agent 推送的实时性能快照。
/// </summary>
public class MetricsSnapshot
{
    public string DeviceId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public CpuMetrics Cpu { get; set; } = new();
    public MemoryMetrics Memory { get; set; } = new();
    public DiskIOMetrics DiskIO { get; set; } = new();
    public NetworkMetrics Network { get; set; } = new();
    public GpuMetrics? Gpu { get; set; }

    /// <summary>当前进程列表 (轻量快照: PID/Name/Memory)</summary>
    public List<ProcessSnapshot> Processes { get; set; } = [];

    /// <summary>当前 TCP 连接列表</summary>
    public List<TcpConnectionInfo> TcpConnections { get; set; } = [];
}
