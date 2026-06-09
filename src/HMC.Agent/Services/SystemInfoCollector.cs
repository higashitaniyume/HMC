using System.Diagnostics;
using System.Management;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using HMC.Shared.Models;
using Microsoft.Extensions.Logging;

namespace HMC.Agent.Services;

public interface ISystemInfoCollector
{
    Task<SystemOverview> CollectAsync(CancellationToken ct = default);
    List<ProcessSnapshot> GetProcesses();
}

public class SystemInfoCollector : ISystemInfoCollector
{
    private readonly ILogger<SystemInfoCollector> _log;

    public SystemInfoCollector(ILogger<SystemInfoCollector> log)
    {
        _log = log;
    }

    public async Task<SystemOverview> CollectAsync(CancellationToken ct = default)
    {
        var overview = new SystemOverview();

        try
        {
            // OS Info
            using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_OperatingSystem"))
            {
                foreach (var obj in searcher.Get().Cast<ManagementObject>())
                {
                    overview.OsName = (string)obj["Caption"];
                    overview.OsVersion = (string)obj["Version"];
                    overview.OsArchitecture = (string)obj["OSArchitecture"];
                    overview.LastBootTime = ManagementDateTimeConverter.ToDateTime((string)obj["LastBootUpTime"]);
                    break;
                }
            }

            overview.TimeZone = TimeZoneInfo.Local.DisplayName;

            // CPU Info
            using (var searcher = new ManagementObjectSearcher("SELECT Name, NumberOfCores, NumberOfLogicalProcessors FROM Win32_Processor"))
            {
                foreach (var obj in searcher.Get().Cast<ManagementObject>())
                {
                    overview.CpuName = (string)obj["Name"];
                    overview.CpuPhysicalCores = Convert.ToInt32(obj["NumberOfCores"]);
                    overview.CpuLogicalCores = Convert.ToInt32(obj["NumberOfLogicalProcessors"]);
                    break;
                }
            }

            // Memory
            using (var searcher = new ManagementObjectSearcher("SELECT TotalVisibleMemorySize FROM Win32_OperatingSystem"))
            {
                foreach (var obj in searcher.Get().Cast<ManagementObject>())
                {
                    overview.TotalMemoryBytes = Convert.ToInt64(obj["TotalVisibleMemorySize"]) * 1024;
                    break;
                }
            }

            // Motherboard / BIOS
            try
            {
                using var boardSearcher = new ManagementObjectSearcher("SELECT Manufacturer, Product FROM Win32_BaseBoard");
                foreach (var obj in boardSearcher.Get().Cast<ManagementObject>())
                {
                    overview.MotherboardManufacturer = (string)obj["Manufacturer"];
                    overview.MotherboardProduct = (string)obj["Product"];
                    break;
                }
                using var biosSearcher = new ManagementObjectSearcher("SELECT SMBIOSBIOSVersion FROM Win32_BIOS");
                foreach (var obj in biosSearcher.Get().Cast<ManagementObject>())
                {
                    overview.BiosVersion = (string)obj["SMBIOSBIOSVersion"];
                    break;
                }
            }
            catch { /* non-critical */ }

            // GPU
            using (var searcher = new ManagementObjectSearcher("SELECT Name, DriverVersion, AdapterRAM FROM Win32_VideoController"))
            {
                foreach (var obj in searcher.Get().Cast<ManagementObject>())
                {
                    overview.Gpus.Add(new GpuInfo
                    {
                        Name = (string)obj["Name"],
                        DriverVersion = obj["DriverVersion"]?.ToString() ?? "",
                        AdapterRamBytes = obj["AdapterRAM"] != null ? Convert.ToInt64(obj["AdapterRAM"]) : 0
                    });
                }
            }

            // Disks
            using (var searcher = new ManagementObjectSearcher("SELECT Model, Size, MediaType FROM Win32_DiskDrive"))
            {
                foreach (var obj in searcher.Get().Cast<ManagementObject>())
                {
                    overview.Disks.Add(new DiskInfo
                    {
                        Model = obj["Model"]?.ToString() ?? "Unknown",
                        TotalSizeBytes = obj["Size"] != null ? Convert.ToInt64(obj["Size"]) : 0,
                        MediaType = obj["MediaType"]?.ToString() ?? "Unknown"
                    });
                }
            }

            // Network Adapters
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                var info = new NetworkAdapterInfo
                {
                    Name = nic.Name,
                    MacAddress = nic.GetPhysicalAddress().ToString(),
                    SpeedBps = nic.Speed
                };
                foreach (var ip in nic.GetIPProperties().UnicastAddresses)
                {
                    if (ip.Address.AddressFamily == AddressFamily.InterNetwork)
                        info.IpAddresses.Add(ip.Address.ToString());
                }
                overview.NetworkAdapters.Add(info);
            }
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Error collecting system overview");
        }

        return await Task.FromResult(overview);
    }

    public List<ProcessSnapshot> GetProcesses()
    {
        var result = new List<ProcessSnapshot>();
        try
        {
            foreach (var proc in Process.GetProcesses())
            {
                try
                {
                    result.Add(new ProcessSnapshot
                    {
                        Pid = proc.Id,
                        Name = proc.ProcessName,
                        WorkingSetMB = proc.WorkingSet64 / (1024.0 * 1024.0),
                        CpuTimeSeconds = proc.TotalProcessorTime.TotalSeconds
                    });
                }
                catch { /* skip processes we can't access */ }
            }
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Failed to enumerate processes");
        }
        return result;
    }
}
