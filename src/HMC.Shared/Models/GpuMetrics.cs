namespace HMC.Shared.Models;

public class GpuMetrics
{
    public List<PerGpuMetrics> Gpus { get; set; } = [];
}

public class PerGpuMetrics
{
    public string Name { get; set; } = string.Empty;
    public double UtilizationPercent { get; set; }
    public double MemoryUsedMB { get; set; }
    public double MemoryTotalMB { get; set; }
    public double TemperatureCelsius { get; set; }
}
