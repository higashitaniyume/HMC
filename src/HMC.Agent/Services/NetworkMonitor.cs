using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using HMC.Agent.Native;
using HMC.Shared.Models;
using Microsoft.Extensions.Logging;
using static HMC.Agent.Native.Iphlpapi;

namespace HMC.Agent.Services;

/// <summary>
/// 使用 GetExtendedTcpTable 获取 TCP 连接列表。
/// </summary>
public interface INetworkMonitor
{
    List<TcpConnectionInfo> GetTcpConnections();
}

public class NetworkMonitor : INetworkMonitor
{
    private readonly ILogger<NetworkMonitor> _log;

    public NetworkMonitor(ILogger<NetworkMonitor> log)
    {
        _log = log;
    }

    public List<TcpConnectionInfo> GetTcpConnections()
    {
        var result = new List<TcpConnectionInfo>();
        try
        {
            int bufferSize = 0;
            _ = GetExtendedTcpTable(IntPtr.Zero, ref bufferSize, true, AF_INET, TCP_TABLE_OWNER_PID_ALL);

            IntPtr buffer = Marshal.AllocHGlobal(bufferSize);
            try
            {
                uint ret = GetExtendedTcpTable(buffer, ref bufferSize, true, AF_INET, TCP_TABLE_OWNER_PID_ALL);
                if (ret != 0)
                {
                    _log.LogWarning("GetExtendedTcpTable returned {Ret}", ret);
                    return result;
                }

                int entryCount = Marshal.ReadInt32(buffer);
                int rowSize = Marshal.SizeOf<MibTcpRowOwnerPid>();
                IntPtr rowPtr = buffer + 4;

                // 缓存进程名，避免重复查询
                var pidNameCache = new Dictionary<int, string>();

                for (int i = 0; i < entryCount; i++)
                {
                    var row = Marshal.PtrToStructure<MibTcpRowOwnerPid>(rowPtr + i * rowSize);

                    if (!pidNameCache.TryGetValue(row.owningPid, out var processName))
                    {
                        processName = GetProcessName(row.owningPid);
                        pidNameCache[row.owningPid] = processName;
                    }

                    result.Add(new TcpConnectionInfo
                    {
                        LocalAddress = UIntToIp(row.localAddr),
                        LocalPort = PortToInt(row.localPort),
                        RemoteAddress = UIntToIp(row.remoteAddr),
                        RemotePort = PortToInt(row.remotePort),
                        State = row.state.ToString(),
                        Pid = row.owningPid,
                        ProcessName = processName
                    });
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Failed to enumerate TCP connections");
        }

        return result;
    }

    private static string GetProcessName(int pid)
    {
        try
        {
            if (pid == 0) return "System Idle Process";
            if (pid == 4) return "System";
            using var proc = Process.GetProcessById(pid);
            return proc.ProcessName;
        }
        catch
        {
            return $"PID:{pid}";
        }
    }

    private static string UIntToIp(uint addr)
    {
        var bytes = BitConverter.GetBytes(addr);
        return $"{bytes[0]}.{bytes[1]}.{bytes[2]}.{bytes[3]}";
    }

    private static int PortToInt(int port)
    {
        // Windows 存储端口为网络字节序
        return IPAddress.NetworkToHostOrder((short)(port & 0xFFFF));
    }
}
