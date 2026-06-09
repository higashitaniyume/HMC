namespace HMC.Shared.Constants;

/// <summary>
/// UDP 发现协议的常量。
/// Agent 广播 "HMC_DISCOVER" → Server 回复 "HMC_SERVER|{ip}:{port}"
/// </summary>
public static class DiscoveryProtocol
{
    public const int Port = 9556;
    public const string DiscoveryMessage = "HMC_DISCOVER";
    public const string ResponsePrefix = "HMC_SERVER|";
}
