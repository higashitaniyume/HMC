using HMC.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace HMC.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MetricsController : ControllerBase
{
    private readonly MetricsStoreService _metricsStore;

    public MetricsController(MetricsStoreService metricsStore)
    {
        _metricsStore = metricsStore;
    }

    /// <summary>查询设备历史指标</summary>
    [HttpGet("{deviceId}/history")]
    public async Task<IActionResult> GetHistory(
        string deviceId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to)
    {
        var fromDate = from ?? DateTime.UtcNow.AddHours(-1);
        var toDate = to ?? DateTime.UtcNow;

        var data = await _metricsStore.GetHistoryAsync(deviceId, fromDate, toDate);
        return Ok(data);
    }
}
