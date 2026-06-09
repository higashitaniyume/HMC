namespace HMC.Shared.Models;

public class PingTarget
{
    public string Address { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;

    public PingTarget() { }

    public PingTarget(string address, string label)
    {
        Address = address;
        Label = label;
    }
}

public class PingResult
{
    public string Address { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public bool Success { get; set; }
    public long RoundTripMs { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public int Sent { get; set; } = 4;
    public int Received { get; set; }
    public int Lost { get; set; }
    public long MinMs { get; set; }
    public long MaxMs { get; set; }
    public long AvgMs { get; set; }
}
