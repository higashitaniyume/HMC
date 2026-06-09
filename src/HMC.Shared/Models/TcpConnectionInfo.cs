namespace HMC.Shared.Models;

/// <summary>
/// TCP 连接信息 (对应 GetExtendedTcpTable 返回的结构)。
/// </summary>
public class TcpConnectionInfo
{
    public string LocalAddress { get; set; } = string.Empty;
    public int LocalPort { get; set; }
    public string RemoteAddress { get; set; } = string.Empty;
    public int RemotePort { get; set; }
    public string State { get; set; } = string.Empty;
    public int Pid { get; set; }
    public string ProcessName { get; set; } = string.Empty;
}
