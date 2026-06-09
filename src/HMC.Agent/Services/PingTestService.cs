using System.Net.NetworkInformation;
using HMC.Shared.Models;
using Microsoft.Extensions.Logging;

namespace HMC.Agent.Services;

public interface IPingTestService
{
    Task<List<PingResult>> RunPingAsync(List<PingTarget> targets, CancellationToken ct = default);
}

public class PingTestService : IPingTestService
{
    private readonly ILogger<PingTestService> _log;

    public PingTestService(ILogger<PingTestService> log)
    {
        _log = log;
    }

    public async Task<List<PingResult>> RunPingAsync(List<PingTarget> targets, CancellationToken ct = default)
    {
        var results = new List<PingResult>();

        foreach (var target in targets)
        {
            _log.LogInformation("Pinging {Label} ({Address})", target.Label, target.Address);
            var result = new PingResult
            {
                Address = target.Address,
                Label = target.Label,
                Sent = 4
            };

            try
            {
                using var ping = new Ping();
                var roundTrips = new List<long>();

                for (int i = 0; i < 4; i++)
                {
                    try
                    {
                        var reply = await ping.SendPingAsync(target.Address, 3000);
                        if (reply.Status == IPStatus.Success)
                        {
                            roundTrips.Add(reply.RoundtripTime);
                        }
                    }
                    catch (Exception ex)
                    {
                        _log.LogDebug(ex, "Ping #{Attempt} to {Address} failed", i + 1, target.Address);
                    }

                    if (i < 3) await Task.Delay(200, ct);
                }

                result.Received = roundTrips.Count;
                result.Lost = 4 - roundTrips.Count;
                result.Success = roundTrips.Count > 0;

                if (roundTrips.Count > 0)
                {
                    result.MinMs = roundTrips.Min();
                    result.MaxMs = roundTrips.Max();
                    result.AvgMs = (long)roundTrips.Average();
                    result.RoundTripMs = result.AvgMs;
                }
                else
                {
                    result.ErrorMessage = "All pings failed";
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
                result.Lost = 4;
                _log.LogWarning(ex, "Ping to {Address} failed", target.Address);
            }

            results.Add(result);
        }

        return results;
    }
}
