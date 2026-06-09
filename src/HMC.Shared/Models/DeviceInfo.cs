namespace HMC.Shared.Models;

/// <summary>
/// 设备基本信息，Agent 启动时上报。
/// </summary>
public class DeviceInfo
{
    /// <summary>设备唯一标识 (GUID)</summary>
    public string DeviceId { get; set; } = Guid.NewGuid().ToString("D");

    /// <summary>自定义名称</summary>
    public string Name { get; set; } = Environment.MachineName;

    /// <summary>主机名</summary>
    public string Hostname { get; set; } = Environment.MachineName;

    /// <summary>局域网 IP</summary>
    public string IpAddress { get; set; } = string.Empty;

    /// <summary>操作系统版本</summary>
    public string OsVersion { get; set; } = string.Empty;

    /// <summary>Agent 版本</summary>
    public string AgentVersion { get; set; } = string.Empty;
}
