namespace HMC.Shared.Models;

public class ProcessSnapshot
{
    public int Pid { get; set; }
    public string Name { get; set; } = string.Empty;
    public double WorkingSetMB { get; set; }
    public double CpuTimeSeconds { get; set; }
}
