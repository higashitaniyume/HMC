using HMC.Agent.Services;
using HMC.Shared.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HMC.Agent.Tests.Services;

public class Iperf3ServiceTests
{
    [Fact]
    public void Constructor_ChecksAvailability()
    {
        var service = new Iperf3Service(NullLogger<Iperf3Service>.Instance);

        // iPerf3.exe may or may not be available in test environment
        // Just verify the service initializes without throwing
        Assert.NotNull(service);
        Assert.NotNull(service.Version);
    }

    [Fact]
    public async Task RunClient_InvalidHost_ReturnsError()
    {
        var service = new Iperf3Service(NullLogger<Iperf3Service>.Instance);

        if (!service.IsAvailable)
        {
            // Skip if iPerf3 not installed
            return;
        }

        var request = new Iperf3TestRequest
        {
            TestId = "test-001",
            SourceDeviceId = "src",
            TargetDeviceId = "dst",
            Mode = Iperf3Mode.Client,
            Port = 15299,
            TargetIp = "192.0.2.1",
            ParallelThreads = 1,
            DurationSeconds = 2
        };

        var result = await service.RunClientAsync(request);

        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.NotEmpty(result.ErrorMessage);
    }
}
