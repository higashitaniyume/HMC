using System.Net;
using System.Net.Sockets;
using System.Text;
using HMC.Shared.Constants;
using Microsoft.Extensions.Logging;

namespace HMC.Agent.Services;

/// <summary>
/// Agent 端：发送 UDP 广播，发现局域网中的 HMC Server。
/// </summary>
public static class ServerDiscovery
{
    /// <summary>
    /// 通过 UDP 广播发现 Server 地址。超时返回 null。
    /// </summary>
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
                        log.LogInformation("Server discovered: http://{Address}", address);
                        return $"http://{address}";
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Server discovery failed");
        }

        log.LogWarning("No HMC Server found via discovery after {Timeout}ms", timeoutMs);
        return null;
    }
}
