namespace HMC.Shared.Models;

public class NetworkTestRequest
{
    /// <summary>测试类型</summary>
    public string TestType { get; set; } = string.Empty; // "PingAll", "PingSpecific", "Iperf3", "PortCheck"

    /// <summary>触发测试的前端连接 ID (用于只推送给发起者)</summary>
    public string? InitiatorConnectionId { get; set; }

    // Ping specific
    public List<PingTarget>? PingTargets { get; set; }

    // iPerf3 specific
    public string? Iperf3SourceDeviceId { get; set; }
    public string? Iperf3TargetDeviceId { get; set; }
    public int Iperf3Port { get; set; } = 5201;
    public int Iperf3Threads { get; set; } = 4;
    public int Iperf3Duration { get; set; } = 10;

    // Port check specific
    public string? PortCheckHost { get; set; }
    public int PortCheckPort { get; set; }
}
