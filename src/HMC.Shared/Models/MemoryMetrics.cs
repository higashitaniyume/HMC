namespace HMC.Shared.Models;

public class MemoryMetrics
{
    /// <summary>总物理内存 (MB)</summary>
    public double TotalMB { get; set; }

    /// <summary>已用物理内存 (MB)</summary>
    public double UsedMB { get; set; }

    /// <summary>可用物理内存 (MB)</summary>
    public double AvailableMB { get; set; }

    /// <summary>使用率 (0-100)</summary>
    public double PercentUsed { get; set; }

    /// <summary>已用交换/页面文件 (MB)</summary>
    public double SwapUsedMB { get; set; }

    /// <summary>总交换/页面文件 (MB)</summary>
    public double SwapTotalMB { get; set; }
}
