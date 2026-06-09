using Xunit;

namespace HMC.Server.Tests.Services;

public class NetworkTestOrchestratorTests
{
    [Fact]
    public void PingTarget_Creation()
    {
        var target = new HMC.Shared.Models.PingTarget("8.8.8.8", "Google DNS");
        Assert.Equal("8.8.8.8", target.Address);
        Assert.Equal("Google DNS", target.Label);
    }

    [Fact]
    public void Iperf3TestRequest_Defaults()
    {
        var req = new HMC.Shared.Models.Iperf3TestRequest
        {
            SourceDeviceId = "A",
            TargetDeviceId = "B",
            TargetIp = "192.168.1.10"
        };

        Assert.Equal("A", req.SourceDeviceId);
        Assert.Equal("B", req.TargetDeviceId);
        Assert.Equal(5201, req.Port);
        Assert.Equal(4, req.ParallelThreads);
        Assert.Equal(10, req.DurationSeconds);
        Assert.False(string.IsNullOrEmpty(req.TestId));
    }
}
