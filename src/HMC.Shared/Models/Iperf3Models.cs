namespace HMC.Shared.Models;

/// <summary>
/// Server 下发给 Agent 的 iPerf3 测试指令。
/// </summary>
public class Iperf3TestRequest
{
    /// <summary>测试 ID (GUID)，用于关联结果</summary>
    public string TestId { get; set; } = Guid.NewGuid().ToString("N")[..8];

    /// <summary>测试方向来源设备 ID</summary>
    public string SourceDeviceId { get; set; } = string.Empty;

    /// <summary>测试方向目标设备 ID</summary>
    public string TargetDeviceId { get; set; } = string.Empty;

    /// <summary>模式: Server 或 Client</summary>
    public Iperf3Mode Mode { get; set; }

    /// <summary>监听端口</summary>
    public int Port { get; set; } = 5201;

    /// <summary>目标 IP (Client 模式填写)</summary>
    public string TargetIp { get; set; } = string.Empty;

    /// <summary>并行线程数</summary>
    public int ParallelThreads { get; set; } = 4;

    /// <summary>测试时长 (秒)</summary>
    public int DurationSeconds { get; set; } = 10;
}

public enum Iperf3Mode
{
    Server,
    Client
}

/// <summary>
/// Agent 回传的 iPerf3 测试结果。
/// </summary>
public class Iperf3Result
{
    public string TestId { get; set; } = string.Empty;
    public string SourceDeviceId { get; set; } = string.Empty;
    public string TargetDeviceId { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;

    // iPerf3 JSON 输出的关键字段
    public double BitsPerSecond { get; set; }
    public double Retransmits { get; set; }
    public double JitterMs { get; set; }
    public long BytesTransferred { get; set; }

    /// <summary>原始 JSON 输出 (保留完整信息)</summary>
    public string RawJson { get; set; } = string.Empty;

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
