namespace HMC.Shared.Models;

public class NetworkMetrics
{
    /// <summary>总接收速率 (bytes/s)</summary>
    public double InBps { get; set; }

    /// <summary>总发送速率 (bytes/s)</summary>
    public double OutBps { get; set; }

    /// <summary>总接收包速率 (packets/s)</summary>
    public double InPps { get; set; }

    /// <summary>总发送包速率 (packets/s)</summary>
    public double OutPps { get; set; }

    /// <summary>每网卡详情</summary>
    public List<PerNicMetrics> Nics { get; set; } = [];
}

public class PerNicMetrics
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public double InBps { get; set; }
    public double OutBps { get; set; }
    public double InPps { get; set; }
    public double OutPps { get; set; }
    public long TotalInBytes { get; set; }
    public long TotalOutBytes { get; set; }
}
