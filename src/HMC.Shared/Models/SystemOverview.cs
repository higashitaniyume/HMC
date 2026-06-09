namespace HMC.Shared.Models;

/// <summary>
/// 系统概述信息：OS 详情、硬件配置等。
/// </summary>
public class SystemOverview
{
    public string DeviceId { get; set; } = string.Empty;

    // OS
    public string OsName { get; set; } = string.Empty;
    public string OsVersion { get; set; } = string.Empty;
    public string OsArchitecture { get; set; } = string.Empty;
    public DateTime LastBootTime { get; set; }
    public string TimeZone { get; set; } = string.Empty;

    // CPU
    public string CpuName { get; set; } = string.Empty;
    public int CpuPhysicalCores { get; set; }
    public int CpuLogicalCores { get; set; }

    // Memory
    public long TotalMemoryBytes { get; set; }

    // Motherboard/BIOS
    public string MotherboardManufacturer { get; set; } = string.Empty;
    public string MotherboardProduct { get; set; } = string.Empty;
    public string BiosVersion { get; set; } = string.Empty;

    // GPU
    public List<GpuInfo> Gpus { get; set; } = [];

    // Disks
    public List<DiskInfo> Disks { get; set; } = [];

    // Network adapters
    public List<NetworkAdapterInfo> NetworkAdapters { get; set; } = [];
}

public class GpuInfo
{
    public string Name { get; set; } = string.Empty;
    public string DriverVersion { get; set; } = string.Empty;
    public long AdapterRamBytes { get; set; }
}

public class DiskInfo
{
    public string Model { get; set; } = string.Empty;
    public string DriveLetter { get; set; } = string.Empty;
    public long TotalSizeBytes { get; set; }
    public string MediaType { get; set; } = string.Empty; // HDD / SSD / NVMe
}

public class NetworkAdapterInfo
{
    public string Name { get; set; } = string.Empty;
    public string MacAddress { get; set; } = string.Empty;
    public List<string> IpAddresses { get; set; } = [];
    public long SpeedBps { get; set; }
}
