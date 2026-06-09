using System.Net;
using System.Net.NetworkInformation;
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
    private readonly string _advertiseIp;

    public DiscoveryService(ILogger<DiscoveryService> log, IConfiguration config)
    {
        _log = log;
        _serverPort = config.GetValue<int?>("Discovery:Port") ?? 5000;
        // Allow explicit override via config/env
        _advertiseIp = config.GetValue<string>("Discovery:AdvertiseIp")
            ?? Environment.GetEnvironmentVariable("HMC_SERVER_IP")
            ?? GetBestLanIp();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var udp = new UdpClient(new IPEndPoint(IPAddress.Any, DiscoveryProtocol.Port));
        udp.EnableBroadcast = true;

        _log.LogInformation("Discovery service on UDP:{DiscoveryPort}, advertising {Ip}:{ServerPort}",
            DiscoveryProtocol.Port, _advertiseIp, _serverPort);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var result = await udp.ReceiveAsync(stoppingToken);
                var message = Encoding.UTF8.GetString(result.Buffer);

                if (message == DiscoveryProtocol.DiscoveryMessage)
                {
                    var response = $"{DiscoveryProtocol.ResponsePrefix}{_advertiseIp}:{_serverPort}";
                    var responseBytes = Encoding.UTF8.GetBytes(response);

                    await udp.SendAsync(responseBytes, result.RemoteEndPoint, stoppingToken);

                    _log.LogInformation("Discovery response → {Remote}: {Response}",
                        result.RemoteEndPoint, response);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    /// <summary>
    /// 从真实物理网卡获取局域网 IP，跳过 Docker/虚拟/VPN 接口。
    /// 返回找到的最佳 IP，无可用接口则返回 127.0.0.1。
    /// </summary>
    public static string GetBestLanIp()
    {
        // Skip these interface types/names
        var skipPatterns = new[] { "docker", "veth", "br-", "tun", "lo", "vEthernet", "Hyper-V", "VirtualBox" };

        try
        {
            var candidates = new List<(string Ip, long Speed)>();

            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                var name = nic.Name;
                if (skipPatterns.Any(p => name.Contains(p, StringComparison.OrdinalIgnoreCase)))
                    continue;
                if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                    continue;
                if (nic.OperationalStatus != OperationalStatus.Up)
                    continue;

                foreach (var ip in nic.GetIPProperties().UnicastAddresses)
                {
                    if (ip.Address.AddressFamily != AddressFamily.InterNetwork)
                        continue;
                    var s = ip.Address.ToString();
                    // Exclude virtual/docker/test ranges
                    if (s.StartsWith("127.") || s.StartsWith("198.18.") || s.StartsWith("198.19.")
                        || s.StartsWith("169.254.") || s.StartsWith("172.17."))
                        continue;

                    candidates.Add((s, nic.Speed));
                }
            }

            if (candidates.Count > 0)
            {
                // Prefer fastest NIC (most likely the real one)
                return candidates.OrderByDescending(c => c.Speed).First().Ip;
            }
        }
        catch { }

        // Last resort
        try
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0);
            socket.Connect("8.8.8.8", 65530);
            if (socket.LocalEndPoint is IPEndPoint ep && !ep.Address.ToString().StartsWith("198.18."))
                return ep.Address.ToString();
        }
        catch { }

        return "127.0.0.1";
    }
}
