using System.Net;
using System.Net.Sockets;
using System.Text;
using HMC.Shared.Constants;
using Microsoft.Extensions.Logging;

namespace HMC.Agent.Services;

/// <summary>
/// Agent 端：发送 UDP 广播，发现局域网中的 HMC Server。
/// 收集所有回复，优先选择局域网 IP（192.168/10.x/172.16-31），排除虚拟网卡 IP。
/// </summary>
public static class ServerDiscovery
{
    private static readonly HashSet<string> BadPrefixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "127.", "198.18.", "198.19.", "169.254.", "0." // loopback, benchmark, link-local
    };

    public static async Task<string?> DiscoverAsync(ILogger log, int timeoutMs = 3000, CancellationToken ct = default)
    {
        log.LogInformation("Discovering HMC Server via UDP broadcast...");

        using var udp = new UdpClient();
        udp.EnableBroadcast = true;

        var requestBytes = Encoding.UTF8.GetBytes(DiscoveryProtocol.DiscoveryMessage);
        var broadcastEp = new IPEndPoint(IPAddress.Broadcast, DiscoveryProtocol.Port);

        try
        {
            await udp.SendAsync(requestBytes, broadcastEp, ct);
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Failed to send discovery broadcast");
            return null;
        }

        // Collect all responses within the timeout
        var responses = new List<string>();
        using var receiveCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        receiveCts.CancelAfter(timeoutMs);

        while (!receiveCts.IsCancellationRequested)
        {
            try
            {
                var result = await udp.ReceiveAsync(receiveCts.Token);
                var message = Encoding.UTF8.GetString(result.Buffer);

                if (message.StartsWith(DiscoveryProtocol.ResponsePrefix))
                {
                    var address = message[DiscoveryProtocol.ResponsePrefix.Length..];
                    responses.Add(address);
                    log.LogInformation("  Found server: http://{Address} (from {IP})",
                        address, result.RemoteEndPoint);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        if (responses.Count == 0)
        {
            log.LogWarning("No HMC Server found via discovery");
            return null;
        }

        // Prioritize: real LAN IPs first, then anything else
        string? best = null;
        foreach (var r in responses)
        {
            var ip = r.Split(':')[0];
            if (!BadPrefixes.Any(p => ip.StartsWith(p)))
            {
                best = r;
                break; // First real LAN IP wins
            }
        }

        // Fallback: first response (even if bad)
        best ??= responses[0];

        log.LogInformation("Server selected: http://{Address}", best);
        return $"http://{best}";
    }
}
