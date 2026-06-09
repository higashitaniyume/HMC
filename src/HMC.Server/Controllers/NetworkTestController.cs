using HMC.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace HMC.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NetworkTestController : ControllerBase
{
    private readonly NetworkTestOrchestrator _orchestrator;

    public NetworkTestController(NetworkTestOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    /// <summary>触发全量 Ping 测试</summary>
    [HttpPost("ping-all")]
    public async Task<IActionResult> PingAll()
    {
        _ = Task.Run(() => _orchestrator.PingAllAsync());
        return Ok(new { Status = "PingAll triggered" });
    }

    /// <summary>触发 iPerf3 双向测速</summary>
    [HttpPost("iperf3")]
    public async Task<IActionResult> Iperf3Test(
        [FromQuery] string source,
        [FromQuery] string target,
        [FromQuery] int threads = 4,
        [FromQuery] int duration = 10)
    {
        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(target))
            return BadRequest("source and target device IDs are required");

        _ = Task.Run(() => _orchestrator.Iperf3TestAsync(source, target, threads, duration));
        return Ok(new { Status = "Iperf3 test triggered", Source = source, Target = target });
    }
}
