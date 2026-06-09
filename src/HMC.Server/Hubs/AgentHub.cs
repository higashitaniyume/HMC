using System.Collections.Concurrent;
using HMC.Server.Data;
using HMC.Server.Services;
using HMC.Shared.Constants;
using HMC.Shared.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace HMC.Server.Hubs;

/// <summary>
/// SignalR Hub：Agent 连接管理 + 实时数据收发。
/// </summary>
public class AgentHub : Hub
{
    private readonly ILogger<AgentHub> _log;
    private readonly DeviceManager _deviceManager;
    private readonly MetricsStoreService _metricsStore;
    private readonly IServiceScopeFactory _scopeFactory;

    // 静态连接表: DeviceId → ConnectionId
    public static readonly ConcurrentDictionary<string, string> DeviceConnections = new();

    public AgentHub(
        ILogger<AgentHub> log,
        DeviceManager deviceManager,
        MetricsStoreService metricsStore,
        IServiceScopeFactory scopeFactory)
    {
        _log = log;
        _deviceManager = deviceManager;
        _metricsStore = metricsStore;
        _scopeFactory = scopeFactory;
    }

    // ===== Agent → Server =====

    /// <summary>Agent 注册/上线</summary>
    public async Task RegisterDevice(DeviceInfo deviceInfo, SystemOverview overview)
    {
        var deviceId = deviceInfo.DeviceId;
        DeviceConnections[deviceId] = Context.ConnectionId;

        await _deviceManager.RegisterAsync(deviceInfo, overview, Context.ConnectionId);

        await Groups.AddToGroupAsync(Context.ConnectionId, "agents");
        await Groups.AddToGroupAsync(Context.ConnectionId, $"device:{deviceId}");

        _log.LogInformation("Device registered: {Name} ({DeviceId}) @ {Ip} - Conn:{ConnId}",
            deviceInfo.Name, deviceId, deviceInfo.IpAddress, Context.ConnectionId);

        // 广播设备列表更新
        await Clients.Group("frontend").SendAsync(HubMethods.DevicesUpdated,
            await _deviceManager.GetAllAsync());
    }

    /// <summary>Agent 推送性能快照</summary>
    public async Task PushMetrics(MetricsSnapshot snapshot)
    {
        // 更新设备心跳
        if (DeviceConnections.TryGetValue(snapshot.DeviceId, out _))
        {
            await _deviceManager.UpdateLastSeenAsync(snapshot.DeviceId);
        }

        // 广播给所有前端客户端
        await Clients.Group("frontend").SendAsync(HubMethods.MetricsUpdated, snapshot);

        // 异步存储 (每分钟聚合)
        _ = _metricsStore.StoreSnapshotAsync(snapshot);
    }

    /// <summary>Agent 提交 Ping 结果</summary>
    public async Task SubmitPingResults(string deviceId, List<PingResult> results)
    {
        _log.LogInformation("Ping results from {DeviceId}: {Count} targets", deviceId, results.Count);

        // 存储
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        foreach (var r in results)
        {
            db.NetworkTestResults.Add(new NetworkTestResultEntity
            {
                DeviceId = deviceId,
                TestType = "Ping",
                Target = $"{r.Label} ({r.Address})",
                ResultJson = System.Text.Json.JsonSerializer.Serialize(r),
                Timestamp = DateTime.UtcNow
            });
        }
        await db.SaveChangesAsync();

        // 广播给前端
        await Clients.Group("frontend").SendAsync(HubMethods.NetworkTestResult,
            new { DeviceId = deviceId, TestType = "Ping", Results = results });
    }

    /// <summary>Agent 提交 iPerf3 结果</summary>
    public async Task SubmitIperf3Result(Iperf3Result result)
    {
        _log.LogInformation("iPerf3 result: {Src}→{Dst} {Speed:F2} Mbps {Success}",
            result.SourceDeviceId, result.TargetDeviceId,
            result.BitsPerSecond / 1_000_000, result.Success);

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.NetworkTestResults.Add(new NetworkTestResultEntity
        {
            DeviceId = result.SourceDeviceId,
            TestType = "Iperf3",
            Target = result.TargetDeviceId,
            ResultJson = System.Text.Json.JsonSerializer.Serialize(result),
            Timestamp = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        await Clients.Group("frontend").SendAsync(HubMethods.NetworkTestResult,
            new { TestType = "Iperf3", Result = result });
    }

    /// <summary>Agent 提交系统信息</summary>
    public async Task SubmitSystemInfo(string deviceId, SystemOverview overview)
    {
        await _deviceManager.UpdateSystemInfoAsync(deviceId, overview);
        await Clients.Group("frontend").SendAsync(HubMethods.SystemInfoUpdated,
            new { DeviceId = deviceId, Overview = overview });
    }

    // ===== 连接管理 =====

    public override async Task OnConnectedAsync()
    {
        var httpCtx = Context.GetHttpContext();
        var userAgent = httpCtx?.Request.Headers.UserAgent.ToString() ?? "";

        // 判断是 Agent 还是前端
        if (httpCtx?.Request.Headers.ContainsKey("X-Device-Id") == true)
        {
            _log.LogInformation("Agent connecting: {DeviceId} Conn:{ConnId}",
                httpCtx.Request.Headers["X-Device-Id"], Context.ConnectionId);
        }
        else
        {
            // 前端客户端加入 frontend 组
            await Groups.AddToGroupAsync(Context.ConnectionId, "frontend");
            _log.LogInformation("Frontend connected: {ConnId}", Context.ConnectionId);
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        // 找到并更新对应设备状态
        var deviceId = DeviceConnections
            .FirstOrDefault(kv => kv.Value == Context.ConnectionId).Key;

        if (!string.IsNullOrEmpty(deviceId))
        {
            DeviceConnections.TryRemove(deviceId, out _);
            await _deviceManager.SetOnlineStatusAsync(deviceId, false);

            _log.LogWarning("Agent disconnected: {DeviceId} Conn:{ConnId}",
                deviceId, Context.ConnectionId);

            // 通知前端
            await Clients.Group("frontend").SendAsync(HubMethods.DevicesUpdated,
                await _deviceManager.GetAllAsync());
        }
        else
        {
            // 前端断开
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, "frontend");
            _log.LogInformation("Frontend disconnected: {ConnId}", Context.ConnectionId);
        }

        await base.OnDisconnectedAsync(exception);
    }
}
