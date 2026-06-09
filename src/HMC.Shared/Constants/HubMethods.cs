namespace HMC.Shared.Constants;

/// <summary>
/// SignalR Hub 方法名常量。
/// 注意：SignalR JSON 协议默认 camelCase，方法名需全小写。
/// </summary>
public static class HubMethods
{
    // ===== Agent → Server =====
    public const string RegisterDevice = "registerdevice";
    public const string PushMetrics = "pushmetrics";
    public const string SubmitPingResults = "submitpingresults";
    public const string SubmitIperf3Result = "submitiperf3result";
    public const string SubmitSystemInfo = "submitsysteminfo";

    // ===== Server → Agent =====
    public const string RunPingTest = "runpingtest";
    public const string StartIperf3Server = "startiperf3server";
    public const string RunIperf3Client = "runiperf3client";
    public const string StopIperf3Server = "stopiperf3server";
    public const string CollectSystemInfo = "collectsysteminfo";
    public const string UpdateInterval = "updateinterval";

    // ===== Server → Frontend =====
    public const string DevicesUpdated = "devicesupdated";
    public const string MetricsUpdated = "metricsupdated";
    public const string NetworkTestResult = "networktestresult";
    public const string SystemInfoUpdated = "systeminfoupdated";
}
