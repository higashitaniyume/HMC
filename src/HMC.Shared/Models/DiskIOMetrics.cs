namespace HMC.Shared.Models;

public class DiskIOMetrics
{
    /// <summary>总读取速率 (bytes/s)</summary>
    public double ReadBps { get; set; }

    /// <summary>总写入速率 (bytes/s)</summary>
    public double WriteBps { get; set; }

    /// <summary>总读取操作数 (IOPS)</summary>
    public double ReadIops { get; set; }

    /// <summary>总写入操作数 (IOPS)</summary>
    public double WriteIops { get; set; }

    /// <summary>平均磁盘队列深度</summary>
    public double AvgQueueDepth { get; set; }

    /// <summary>每块物理磁盘的详情</summary>
    public List<PerDiskMetrics> Disks { get; set; } = [];
}

public class PerDiskMetrics
{
    public string Name { get; set; } = string.Empty;
    public string DriveLetter { get; set; } = string.Empty;
    public double ReadBps { get; set; }
    public double WriteBps { get; set; }
    public double DiskTimePercent { get; set; }
}
