using System.Net;
using System.Net.Sockets;
using System.Text;
using HMC.Shared.Constants;

namespace HMC.Server.Services;

/// <summary>
/// 监听 UDP 广播，当 Agent 发送 HMC_DISCOVER 时回复 HMC_SERVER|{ip}:{port}
/// </summary>
public class DiscoveryService : BackgroundService
{
    private readonly ILogger<DiscoveryService> _log;
    private readonly int _serverPort;

    public DiscoveryService(ILogger<DiscoveryService> log, IConfiguration config)
    {
        _log = log;
        _serverPort = config.GetValue<int?>("Discovery:Port") ?? 5000;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var udp = new UdpClient(new IPEndPoint(IPAddress.Any, DiscoveryProtocol.Port));
        udp.EnableBroadcast = true;

        _log.LogInformation("Discovery service listening on UDP:{Port}", DiscoveryProtocol.Port);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var result = await udp.ReceiveAsync(stoppingToken);
                var message = Encoding.UTF8.GetString(result.Buffer);

                if (message == DiscoveryProtocol.DiscoveryMessage)
                {
                    var localIp = GetLocalIp();
                    var response = $"{DiscoveryProtocol.ResponsePrefix}{localIp}:{_serverPort}";
                    var responseBytes = Encoding.UTF8.GetBytes(response);

                    await udp.SendAsync(responseBytes, result.RemoteEndPoint, stoppingToken);

                    _log.LogInformation("Discovery response sent to {Remote}: {Response}",
                        result.RemoteEndPoint, response);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private static string GetLocalIp()
    {
        try
        {
            // Prefer real LAN IPs over virtual/Docker ones
            var host = Dns.GetHostEntry(Dns.GetHostName());
            string? fallback = null;
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily != AddressFamily.InterNetwork) continue;
                var s = ip.ToString();
                // Skip loopback, Docker, and benchmark/test ranges
                if (s.StartsWith("127.") || s.StartsWith("198.18.") || s.StartsWith("198.19.")
                    || s.StartsWith("169.254.") || s.StartsWith("172.17."))
                {
                    fallback ??= s;
                    continue;
                }
                return s; // Best match
            }
            // Fallback to any non-loopback
            if (fallback != null) return fallback;
        }
        catch { }

        // Last resort: routing-based guess
        try
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0);
            socket.Connect("8.8.8.8", 65530);
            if (socket.LocalEndPoint is IPEndPoint ep)
                return ep.Address.ToString();
        }
        catch { }
        return "127.0.0.1";
    }
}
