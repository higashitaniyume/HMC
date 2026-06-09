namespace HMC.Shared.Constants;

/// <summary>
/// SignalR Hub 方法名常量，保持 Agent 和 Server 同步。
/// </summary>
public static class HubMethods
{
    // ===== Agent → Server =====

    /// <summary>Agent 注册/上线</summary>
    public const string RegisterDevice = "RegisterDevice";

    /// <summary>推送性能快照</summary>
    public const string PushMetrics = "PushMetrics";

    /// <summary>提交 Ping 测试结果</summary>
    public const string SubmitPingResults = "SubmitPingResults";

    /// <summary>提交 iPerf3 测试结果</summary>
    public const string SubmitIperf3Result = "SubmitIperf3Result";

    /// <summary>按需回传系统信息</summary>
    public const string SubmitSystemInfo = "SubmitSystemInfo";

    // ===== Server → Agent =====

    /// <summary>要求 Agent 执行 Ping 测试</summary>
    public const string RunPingTest = "RunPingTest";

    /// <summary>要求 Agent 启动 iPerf3 Server</summary>
    public const string StartIperf3Server = "StartIperf3Server";

    /// <summary>要求 Agent 运行 iPerf3 Client</summary>
    public const string RunIperf3Client = "RunIperf3Client";

    /// <summary>要求 Agent 停止 iPerf3 Server</summary>
    public const string StopIperf3Server = "StopIperf3Server";

    /// <summary>要求 Agent 采集系统信息</summary>
    public const string CollectSystemInfo = "CollectSystemInfo";

    /// <summary>要求 Agent 更新采集间隔</summary>
    public const string UpdateInterval = "UpdateInterval";

    // ===== Server → Frontend =====

    /// <summary>设备列表更新 (上线/下线/注册)</summary>
    public const string DevicesUpdated = "DevicesUpdated";

    /// <summary>实时指标数据广播</summary>
    public const string MetricsUpdated = "MetricsUpdated";

    /// <summary>网络测试结果广播</summary>
    public const string NetworkTestResult = "NetworkTestResult";

    /// <summary>系统信息更新</summary>
    public const string SystemInfoUpdated = "SystemInfoUpdated";
}
