using HMC.Server.Hubs;
using HMC.Shared.Constants;
using HMC.Shared.Models;
using Microsoft.AspNetCore.SignalR;

namespace HMC.Server.Services;

public class NetworkTestOrchestrator
{
    private readonly ILogger<NetworkTestOrchestrator> _log;
    private readonly IHubContext<AgentHub> _hub;
    private readonly DeviceManager _deviceManager;
    private readonly ServerIperf3Service _serverIperf;
    private int _nextIperf3Port = 5201;

    public const string ServerDeviceId = "_server_";

    public NetworkTestOrchestrator(
        ILogger<NetworkTestOrchestrator> log,
        IHubContext<AgentHub> hub,
        DeviceManager deviceManager,
        ServerIperf3Service serverIperf)
    {
        _log = log;
        _hub = hub;
        _deviceManager = deviceManager;
        _serverIperf = serverIperf;
    }

    public async Task PingAllAsync(CancellationToken ct = default)
    {
        var devices = await _deviceManager.GetAllAsync();
        var onlineDevices = devices.Where(d => d.IsOnline).ToList();

        if (onlineDevices.Count == 0)
        {
            _log.LogWarning("No online devices for ping test");
            return;
        }

        var internetTargets = new List<PingTarget>
        {
            new("8.8.8.8", "Google DNS"),
            new("1.1.1.1", "Cloudflare DNS"),
            new("google.com", "Google")
        };

        foreach (var device in onlineDevices)
        {
            var targets = new List<PingTarget>
            {
                new(GetServerIp(), "Server")
            };

            foreach (var other in onlineDevices)
            {
                if (other.DeviceId == device.DeviceId) continue;
                targets.Add(new PingTarget(other.IpAddress, other.Name));
            }

            targets.AddRange(internetTargets);

            if (AgentHub.DeviceConnections.TryGetValue(device.DeviceId, out var connId))
            {
                _log.LogInformation("Dispatching Ping to {Device} ({Count} targets)",
                    device.Name, targets.Count);
                await _hub.Clients.Client(connId).SendAsync(HubMethods.RunPingTest, targets, ct);
            }
        }
    }

    /// <summary>
    /// iPerf3 测速。source/target 可以是 Agent DeviceId 或 "_server_"。
    /// </summary>
    public async Task Iperf3TestAsync(string sourceId, string targetId,
        int threads = 4, int duration = 10, CancellationToken ct = default)
    {
        var port = Interlocked.Increment(ref _nextIperf3Port);
        if (port > 5300) Interlocked.Exchange(ref _nextIperf3Port, 5201);
        port = Interlocked.Exchange(ref _nextIperf3Port, port);

        var testId = Guid.NewGuid().ToString("N")[..8];

        // Determine who runs server and who runs client
        if (targetId == ServerDeviceId)
        {
            // Agent → Server
            var agent = await _deviceManager.GetByIdAsync(sourceId);
            if (agent == null || !AgentHub.DeviceConnections.TryGetValue(sourceId, out var conn))
            {
                _log.LogError("iPerf3: agent {Src} not available", sourceId);
                return;
            }

            if (!_serverIperf.IsAvailable)
            {
                _log.LogError("iPerf3: iperf3 not installed on server (apt install iperf3)");
                return;
            }

            _log.LogInformation("iPerf3: {Src} → Server port={Port}", agent.Name, port);

            await _serverIperf.StartServerAsync(port, ct);
            await Task.Delay(500, ct);

            await _hub.Clients.Client(conn).SendAsync(HubMethods.RunIperf3Client, new Iperf3TestRequest
            {
                TestId = testId,
                SourceDeviceId = sourceId,
                TargetDeviceId = ServerDeviceId,
                Mode = Iperf3Mode.Client,
                Port = port,
                TargetIp = GetServerIp(),
                ParallelThreads = threads,
                DurationSeconds = duration
            }, ct);
        }
        else if (sourceId == ServerDeviceId)
        {
            // Server → Agent
            var agent = await _deviceManager.GetByIdAsync(targetId);
            if (agent == null || !AgentHub.DeviceConnections.TryGetValue(targetId, out var conn))
            {
                _log.LogError("iPerf3: agent {Dst} not available", targetId);
                return;
            }

            if (!_serverIperf.IsAvailable)
            {
                _log.LogError("iPerf3: iperf3 not installed on server");
                return;
            }

            _log.LogInformation("iPerf3: Server → {Dst} port={Port}", agent.Name, port);

            // Start server on Agent
            await _hub.Clients.Client(conn).SendAsync(HubMethods.StartIperf3Server, new Iperf3TestRequest
            {
                TestId = testId,
                SourceDeviceId = ServerDeviceId,
                TargetDeviceId = targetId,
                Mode = Iperf3Mode.Server,
                Port = port,
                DurationSeconds = duration
            }, ct);
            await Task.Delay(800, ct);

            // Run client on Server
            var result = await _serverIperf.RunClientAsync(agent.IpAddress, port, threads, duration, ct);
            result.SourceDeviceId = ServerDeviceId;
            result.TargetDeviceId = targetId;
            result.TestId = testId;

            await _serverIperf.StopServerAsync(port);

            await _hub.Clients.Group("frontend").SendAsync(HubMethods.NetworkTestResult,
                new { TestType = "Iperf3", Result = result }, ct);
        }
        else
        {
            // Agent ↔ Agent
            var srcDev = await _deviceManager.GetByIdAsync(sourceId);
            var dstDev = await _deviceManager.GetByIdAsync(targetId);

            if (srcDev == null || dstDev == null
                || !AgentHub.DeviceConnections.TryGetValue(sourceId, out var srcConn)
                || !AgentHub.DeviceConnections.TryGetValue(targetId, out var dstConn))
            {
                _log.LogError("iPerf3: devices not available");
                return;
            }

            _log.LogInformation("iPerf3 Phase 1: {Src} → {Dst} port={Port}",
                srcDev.Name, dstDev.Name, port);

            await _hub.Clients.Client(dstConn).SendAsync(HubMethods.StartIperf3Server, new Iperf3TestRequest
            {
                TestId = testId,
                SourceDeviceId = sourceId,
                TargetDeviceId = targetId,
                Mode = Iperf3Mode.Server,
                Port = port,
                DurationSeconds = duration
            }, ct);
            await Task.Delay(500, ct);

            await _hub.Clients.Client(srcConn).SendAsync(HubMethods.RunIperf3Client, new Iperf3TestRequest
            {
                TestId = testId,
                SourceDeviceId = sourceId,
                TargetDeviceId = targetId,
                Mode = Iperf3Mode.Client,
                Port = port,
                TargetIp = dstDev.IpAddress,
                ParallelThreads = threads,
                DurationSeconds = duration
            }, ct);

            // Phase 2: reverse direction
            _ = Task.Run(async () =>
            {
                await Task.Delay((duration + 3) * 1000, ct);
                int port2 = port + 1;

                var hasSrc = AgentHub.DeviceConnections.TryGetValue(sourceId, out var s);
                var hasDst = AgentHub.DeviceConnections.TryGetValue(targetId, out var d);
                if (!hasSrc || !hasDst) return;

                await _hub.Clients.Client(s!).SendAsync(HubMethods.StartIperf3Server, new Iperf3TestRequest
                {
                    TestId = testId + "-r",
                    SourceDeviceId = targetId,
                    TargetDeviceId = sourceId,
                    Mode = Iperf3Mode.Server,
                    Port = port2,
                    DurationSeconds = duration
                }, ct);
                await Task.Delay(500, ct);
                await _hub.Clients.Client(d!).SendAsync(HubMethods.RunIperf3Client, new Iperf3TestRequest
                {
                    TestId = testId + "-r",
                    SourceDeviceId = targetId,
                    TargetDeviceId = sourceId,
                    Mode = Iperf3Mode.Client,
                    Port = port2,
                    TargetIp = srcDev.IpAddress,
                    ParallelThreads = threads,
                    DurationSeconds = duration
                }, ct);
            }, ct);
        }
    }

    private string GetServerIp()
    {
        return Environment.GetEnvironmentVariable("HMC_SERVER_IP")
            ?? DiscoveryService.GetBestLanIp();
    }
}
