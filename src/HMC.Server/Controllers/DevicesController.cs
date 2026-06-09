using HMC.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace HMC.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DevicesController : ControllerBase
{
    private readonly DeviceManager _deviceManager;

    public DevicesController(DeviceManager deviceManager)
    {
        _deviceManager = deviceManager;
    }

    /// <summary>获取所有设备列表</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var devices = await _deviceManager.GetAllAsync();
        return Ok(devices);
    }

    /// <summary>获取单个设备详情</summary>
    [HttpGet("{deviceId}")]
    public async Task<IActionResult> GetById(string deviceId)
    {
        var device = await _deviceManager.GetByIdAsync(deviceId);
        if (device == null) return NotFound();
        return Ok(device);
    }
}
