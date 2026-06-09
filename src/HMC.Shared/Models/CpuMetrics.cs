namespace HMC.Shared.Models;

public class CpuMetrics
{
    /// <summary>总体 CPU 使用率 (0-100)</summary>
    public double TotalPercent { get; set; }

    /// <summary>每核心使用率</summary>
    public List<double> PerCorePercent { get; set; } = [];

    /// <summary>当前 CPU 频率 (MHz)</summary>
    public double CurrentFrequencyMhz { get; set; }
}
