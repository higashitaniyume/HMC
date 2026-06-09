using System.Net;
using HMC.Shared.Constants;
using HMC.Shared.Models;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HMC.Agent.Services;

/// <summary>
/// 管理与 Server 的 SignalR 持久连接。
/// </summary>
public class SignalRClientService : IAsyncDisposable
{
    private readonly ILogger<SignalRClientService> _log;
    private readonly IServiceProvider _sp;
    private readonly string _serverUrl;
    private readonly string _deviceId;
    private readonly string _deviceName;
    private readonly int _reconnectDelayMs;

    private HubConnection? _connection;
    private CancellationTokenSource? _reconnectCts;
    private bool _disposed;

    public HubConnection? Connection => _connection;
    public bool IsConnected => _connection?.State == HubConnectionState.Connected;

    public event Func<Task>? OnConnected;
    public event Func<Task>? OnDisconnected;

    // Server → Agent 回调注册
    public Func<List<PingTarget>, Task<List<PingResult>>>? OnRunPingTest { get; set; }
    public Func<Iperf3TestRequest, Task>? OnStartIperf3Server { get; set; }
    public Func<Iperf3TestRequest, Task<Iperf3Result>>? OnRunIperf3Client { get; set; }
    public Func<int, Task>? OnStopIperf3Server { get; set; }
    public Func<Task<SystemOverview>>? OnCollectSystemInfo { get; set; }

    public SignalRClientService(
        ILogger<SignalRClientService> log,
        IServiceProvider sp,
        string serverUrl,
        string deviceId,
        string deviceName,
        int reconnectDelayMs = 5000)
    {
        _log = log;
        _sp = sp;
        _serverUrl = serverUrl;
        _deviceId = deviceId;
        _deviceName = deviceName;
        _reconnectDelayMs = reconnectDelayMs;
    }

    public async Task StartAsync(CancellationToken ct = default)
    {
        _log.LogInformation("Starting SignalR client, connecting to {ServerUrl}", _serverUrl);

        _connection = new HubConnectionBuilder()
            .WithUrl(_serverUrl + "/hub/agent", options =>
            {
                options.Headers.Add("X-Device-Id", _deviceId);
                options.Headers.Add("X-Device-Name", Uri.EscapeDataString(_deviceName));
            })
            .WithAutomaticReconnect(new RetryPolicy(_log))
            .AddJsonProtocol()
            .Build();

        RegisterServerCallbacks();

        _connection.Closed += async (error) =>
        {
            _log.LogWarning(error, "SignalR connection closed");
            if (OnDisconnected != null) await OnDisconnected.Invoke();
        };

        _connection.Reconnected += async (connectionId) =>
        {
            _log.LogInformation("SignalR reconnected: {ConnectionId}", connectionId);
            await RegisterWithServer();
            if (OnConnected != null) await OnConnected.Invoke();
        };

        _connection.Reconnecting += (error) =>
        {
            _log.LogWarning(error, "SignalR reconnecting...");
            return Task.CompletedTask;
        };

        try
        {
            await _connection.StartAsync(ct);
            _log.LogInformation("SignalR connected: {ConnectionId}", _connection.ConnectionId);
            await RegisterWithServer();
            if (OnConnected != null) await OnConnected.Invoke();
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to connect to SignalR server. Retrying in {Delay}s...", _reconnectDelayMs / 1000);
            _ = Task.Run(() => AutoReconnectLoop(ct), ct);
        }
    }

    private async Task AutoReconnectLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && !_disposed)
        {
            try
            {
                await Task.Delay(_reconnectDelayMs, ct);
                if (_connection?.State == HubConnectionState.Connected) break;
                await _connection!.StartAsync(ct);
                await RegisterWithServer();
                _log.LogInformation("SignalR reconnected after retry");
                if (OnConnected != null) await OnConnected.Invoke();
                break;
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Reconnect attempt failed, retrying...");
            }
        }
    }

    private async Task RegisterWithServer()
    {
        if (_connection?.State != HubConnectionState.Connected) return;

        try
        {
            // Collect system info for registration
            var sysInfoCollector = _sp.GetRequiredService<ISystemInfoCollector>();
            var overview = await sysInfoCollector.CollectAsync();
            overview.DeviceId = _deviceId;

            // Get local IP
            var localIp = GetLocalIpAddress();

            var deviceInfo = new DeviceInfo
            {
                DeviceId = _deviceId,
                Name = _deviceName,
                Hostname = Environment.MachineName,
                IpAddress = localIp,
                OsVersion = Environment.OSVersion.ToString(),
                AgentVersion = GetType().Assembly.GetName().Version?.ToString() ?? "1.0.0"
            };

            await _connection.InvokeAsync(HubMethods.RegisterDevice, deviceInfo, overview);
            _log.LogInformation("Device registered with server: {DeviceId} @ {Ip}", _deviceId, localIp);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to register device with server");
        }
    }

    private void RegisterServerCallbacks()
    {
        if (_connection == null) return;

        // Server → Agent: Run Pings
        _connection.On<List<PingTarget>>(HubMethods.RunPingTest, async (targets) =>
        {
            _log.LogInformation("Received RunPingTest: {Count} targets", targets.Count);
            if (OnRunPingTest != null)
            {
                var results = await OnRunPingTest(targets);
                await _connection.InvokeAsync(HubMethods.SubmitPingResults, _deviceId, results);
            }
        });

        // Server → Agent: Start iPerf3 Server
        _connection.On<Iperf3TestRequest>(HubMethods.StartIperf3Server, async (request) =>
        {
            _log.LogInformation("Received StartIperf3Server: port={Port}", request.Port);
            if (OnStartIperf3Server != null)
                await OnStartIperf3Server(request);
        });

        // Server → Agent: Run iPerf3 Client
        _connection.On<Iperf3TestRequest>(HubMethods.RunIperf3Client, async (request) =>
        {
            _log.LogInformation("Received RunIperf3Client: target={Target}:{Port}", request.TargetIp, request.Port);
            if (OnRunIperf3Client != null)
            {
                var result = await OnRunIperf3Client(request);
                await _connection.InvokeAsync(HubMethods.SubmitIperf3Result, result);
            }
        });

        // Server → Agent: Stop iPerf3 Server
        _connection.On<int>(HubMethods.StopIperf3Server, async (port) =>
        {
            _log.LogInformation("Received StopIperf3Server: port={Port}", port);
            if (OnStopIperf3Server != null)
                await OnStopIperf3Server(port);
        });

        // Server → Agent: Collect System Info
        _connection.On(HubMethods.CollectSystemInfo, async () =>
        {
            _log.LogInformation("Received CollectSystemInfo request");
            if (OnCollectSystemInfo != null)
            {
                var info = await OnCollectSystemInfo();
                await _connection.InvokeAsync(HubMethods.SubmitSystemInfo, _deviceId, info);
            }
        });
    }

    public async Task PushMetricsAsync(MetricsSnapshot snapshot)
    {
        if (_connection?.State == HubConnectionState.Connected)
        {
            try
            {
                await _connection.InvokeAsync(HubMethods.PushMetrics, snapshot);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Failed to push metrics");
            }
        }
    }

    public async Task StopAsync()
    {
        if (_connection != null)
        {
            try
            {
                await _connection.StopAsync();
                await _connection.DisposeAsync();
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Error stopping SignalR connection");
            }
        }
    }

    private static string GetLocalIpAddress()
    {
        try
        {
            using var socket = new System.Net.Sockets.Socket(
                System.Net.Sockets.AddressFamily.InterNetwork,
                System.Net.Sockets.SocketType.Dgram, 0);
            socket.Connect("8.8.8.8", 65530);
            if (socket.LocalEndPoint is IPEndPoint endPoint)
                return endPoint.Address.ToString();
        }
        catch { }
        return "127.0.0.1";
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        _reconnectCts?.Cancel();
        await StopAsync();
    }

    private class RetryPolicy : IRetryPolicy
    {
        private readonly ILogger _log;
        public RetryPolicy(ILogger log) { _log = log; }

        public TimeSpan? NextRetryDelay(RetryContext retryContext)
        {
            var delay = retryContext.PreviousRetryCount switch
            {
                0 => TimeSpan.FromSeconds(2),
                1 => TimeSpan.FromSeconds(5),
                2 => TimeSpan.FromSeconds(10),
                _ => TimeSpan.FromSeconds(30)
            };
            _log.LogInformation("SignalR retry #{Count} in {Delay}s", retryContext.PreviousRetryCount + 1, delay.TotalSeconds);
            return delay;
        }
    }
}
