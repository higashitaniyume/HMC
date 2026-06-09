using HMC.Server.Hubs;
using HMC.Shared.Constants;
using HMC.Shared.Models;
using Microsoft.AspNetCore.SignalR;

namespace HMC.Server.Services;

/// <summary>
/// 编排 Ping 和 iPerf3 网络测试。
/// </summary>
public class NetworkTestOrchestrator
{
    private readonly ILogger<NetworkTestOrchestrator> _log;
    private readonly IHubContext<AgentHub> _hub;
    private readonly DeviceManager _deviceManager;

    // iPerf3 端口分配 (从 5201 开始递进)
    private int _nextIperf3Port = 5201;

    public NetworkTestOrchestrator(
        ILogger<NetworkTestOrchestrator> log,
        IHubContext<AgentHub> hub,
        DeviceManager deviceManager)
    {
        _log = log;
        _hub = hub;
        _deviceManager = deviceManager;
    }

    /// <summary>
    /// 全量 Ping 测试：所有设备对 设备间 + 服务器 + 互联网目标 进行 Ping。
    /// </summary>
    public async Task PingAllAsync(CancellationToken ct = default)
    {
        var devices = await _deviceManager.GetAllAsync();
        var onlineDevices = devices.Where(d => d.IsOnline).ToList();

        if (onlineDevices.Count == 0)
        {
            _log.LogWarning("No online devices for ping test");
            return;
        }

        // 构建互联网 Ping 目标
        var internetTargets = new List<PingTarget>
        {
            new("8.8.8.8", "Google DNS"),
            new("1.1.1.1", "Cloudflare DNS"),
            new("google.com", "Google")
        };

        // 为每个设备构建 Ping 目标列表
        foreach (var device in onlineDevices)
        {
            var targets = new List<PingTarget>();

            // Ping 服务器
            targets.Add(new PingTarget(GetServerIp(), "Server"));

            // Ping 其他设备
            foreach (var other in onlineDevices)
            {
                if (other.DeviceId == device.DeviceId) continue;
                targets.Add(new PingTarget(other.IpAddress, other.Name));
            }

            // Ping 互联网
            targets.AddRange(internetTargets);

            if (AgentHub.DeviceConnections.TryGetValue(device.DeviceId, out var connId))
            {
                _log.LogInformation("Dispatching Ping test to {Device} ({Count} targets)",
                    device.Name, targets.Count);
                await _hub.Clients.Client(connId).SendAsync(HubMethods.RunPingTest, targets, ct);
            }
        }
    }

    /// <summary>
    /// 指定两台设备间的 iPerf3 双向测速。
    /// </summary>
    public async Task Iperf3TestAsync(string sourceDeviceId, string targetDeviceId,
        int threads = 4, int duration = 10, CancellationToken ct = default)
    {
        var srcDevice = await _deviceManager.GetByIdAsync(sourceDeviceId);
        var dstDevice = await _deviceManager.GetByIdAsync(targetDeviceId);

        if (srcDevice == null || dstDevice == null
            || !AgentHub.DeviceConnections.TryGetValue(sourceDeviceId, out var srcConn)
            || !AgentHub.DeviceConnections.TryGetValue(targetDeviceId, out var dstConn))
        {
            _log.LogError("iPerf3 test failed: devices not available");
            return;
        }

        var port = Interlocked.Increment(ref _nextIperf3Port);
        if (port > 5300) Interlocked.Exchange(ref _nextIperf3Port, 5201);
        port = Interlocked.Exchange(ref _nextIperf3Port, port);

        var testId = Guid.NewGuid().ToString("N")[..8];

        // Phase 1: A → B
        _log.LogInformation("iPerf3 Phase 1: {Src} → {Dst} port={Port}",
            srcDevice.Name, dstDevice.Name, port);

        var serverReq = new Iperf3TestRequest
        {
            TestId = testId,
            SourceDeviceId = sourceDeviceId,
            TargetDeviceId = targetDeviceId,
            Mode = Iperf3Mode.Server,
            Port = port,
            DurationSeconds = duration
        };
        var clientReq = new Iperf3TestRequest
        {
            TestId = testId,
            SourceDeviceId = sourceDeviceId,
            TargetDeviceId = targetDeviceId,
            Mode = Iperf3Mode.Client,
            Port = port,
            TargetIp = dstDevice.IpAddress,
            ParallelThreads = threads,
            DurationSeconds = duration
        };

        // Start server on target
        await _hub.Clients.Client(dstConn).SendAsync(HubMethods.StartIperf3Server, serverReq, ct);
        await Task.Delay(500, ct); // brief delay for server to start

        // Run client on source
        await _hub.Clients.Client(srcConn).SendAsync(HubMethods.RunIperf3Client, clientReq, ct);

        // Phase 2 will be handled after client result (the agent submits result which triggers
        // the reverse direction test in the Hub's SubmitIperf3Result handler)
        // For simplicity, we do Phase 2 here after a delay
        _ = Task.Run(async () =>
        {
            await Task.Delay((duration + 3) * 1000, ct);
            int port2 = port + 1;

            var serverReq2 = new Iperf3TestRequest
            {
                TestId = testId + "-r",
                SourceDeviceId = targetDeviceId,
                TargetDeviceId = sourceDeviceId,
                Mode = Iperf3Mode.Server,
                Port = port2,
                DurationSeconds = duration
            };
            var clientReq2 = new Iperf3TestRequest
            {
                TestId = testId + "-r",
                SourceDeviceId = targetDeviceId,
                TargetDeviceId = sourceDeviceId,
                Mode = Iperf3Mode.Client,
                Port = port2,
                TargetIp = srcDevice.IpAddress,
                ParallelThreads = threads,
                DurationSeconds = duration
            };

            await _hub.Clients.Client(srcConn).SendAsync(HubMethods.StartIperf3Server, serverReq2, ct);
            await Task.Delay(500, ct);
            await _hub.Clients.Client(dstConn).SendAsync(HubMethods.RunIperf3Client, clientReq2, ct);
        }, ct);
    }

    private static string GetServerIp()
    {
        // Server IP: 可由环境变量配置，默认 localhost
        return Environment.GetEnvironmentVariable("HMC_SERVER_IP") ?? "127.0.0.1";
    }
}
