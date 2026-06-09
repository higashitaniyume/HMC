using HMC.Agent.Services;
using HMC.Shared.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HMC.Agent.Tests.Services;

public class PingTestServiceTests
{
    [Fact]
    public async Task Ping_Localhost_ReturnsSuccess()
    {
        var service = new PingTestService(NullLogger<PingTestService>.Instance);
        var targets = new List<PingTarget>
        {
            new("127.0.0.1", "Localhost")
        };

        var results = await service.RunPingAsync(targets);

        Assert.Single(results);
        Assert.True(results[0].Success);
        Assert.True(results[0].AvgMs < 100, $"Expected avg < 100ms, got {results[0].AvgMs}ms");
        Assert.Equal(4, results[0].Sent);
        Assert.Equal(4, results[0].Received);
        Assert.Equal(0, results[0].Lost);
    }

    [Fact]
    public async Task Ping_InvalidHost_ReturnsFailure()
    {
        var service = new PingTestService(NullLogger<PingTestService>.Instance);
        var targets = new List<PingTarget>
        {
            new("192.0.2.1", "TEST-NET-1") // RFC 5737 - unreachable
        };

        var results = await service.RunPingAsync(targets);

        Assert.Single(results);
        Assert.False(results[0].Success);
        Assert.Equal(4, results[0].Sent);
        Assert.Equal(0, results[0].Received);
        Assert.Equal(4, results[0].Lost);
    }

    [Fact]
    public async Task Ping_MultipleTargets_ReturnsAllResults()
    {
        var service = new PingTestService(NullLogger<PingTestService>.Instance);
        var targets = new List<PingTarget>
        {
            new("127.0.0.1", "A"),
            new("127.0.0.1", "B"),
            new("127.0.0.1", "C"),
        };

        var results = await service.RunPingAsync(targets);

        Assert.Equal(3, results.Count);
        Assert.All(results, r => Assert.True(r.Success));
    }
}
